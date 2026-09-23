using Amazon.S3;
using Amazon.S3.Model;
using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Storage;

/// <summary>
///     An <b>Adapter</b> (Gang of Four) over the AWS S3 SDK, which is why
///     <c>HpacSafety.Core</c> can talk about private object storage without knowing
///     AWS exists. It is S3-compatible rather than AWS-specific, so the same class
///     serves S3 in production and the MinIO container development and the contract
///     suite run against; only configuration differs. See ADR-0026 and ADR-0096.
///     <para>
///         A pre-signed URL is signed over the bucket, the key, the verb, and the
///         expiry. Point it at a different key and the signature no longer matches, so
///         S3 answers <c>403</c> — the URL is a capability for one object, not a
///         password for the bucket.
///     </para>
/// </summary>
public sealed class S3BlobStore : IBlobStore
{
	private readonly string _bucketName;
	private readonly TimeProvider _clock;
	private readonly IAmazonS3 _s3;
	private readonly IAmazonS3 _signer;

	/// <summary>Creates the adapter.</summary>
	/// <param name="s3">The client every request to the bucket goes through.</param>
	/// <param name="options">Which bucket.</param>
	/// <param name="clock">The clock a URL's expiry is measured from.</param>
	/// <param name="signer">
	///     The client that signs pre-signed URLs, when the host a browser reaches the
	///     bucket by differs from the host this process does — MinIO in
	///     docker-compose is <c>minio:9000</c> to the API and <c>localhost:9000</c> to
	///     the browser, and SigV4 signs the host. Signing is local, so this client
	///     never sends a request. <see langword="null" /> signs with
	///     <paramref name="s3" />, which is right for S3 itself.
	/// </param>
	public S3BlobStore(IAmazonS3 s3,
					   S3BlobStoreOptions options,
					   TimeProvider clock,
					   IAmazonS3? signer = null)
	{
		ArgumentNullException.ThrowIfNull(s3);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(clock);
		ArgumentException.ThrowIfNullOrWhiteSpace(options.BucketName);

		_s3 = s3;
		_signer = signer ?? s3;
		_bucketName = options.BucketName;
		_clock = clock;
	}

	// The SDK defaults a pre-signed URL to HTTPS regardless of the configured
	// endpoint, which is right for S3 and wrong for an S3-compatible server
	// reached over plain HTTP - MinIO in development and in the contract suite.
	// The scheme is not part of what SigV4 signs, so this decides where the URL
	// points, never whether it is valid. Production has no ServiceURL set and
	// therefore stays on HTTPS.
	private Protocol ConfiguredProtocol =>
		_signer.Config.ServiceURL?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
			? Protocol.HTTP
			: Protocol.HTTPS;

	/// <inheritdoc />
	public async Task<Uri> CreateReadUrl(BlobKey key,
										 string downloadFileName,
										 TimeSpan lifetime,
										 CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(downloadFileName);

		var request = new GetPreSignedUrlRequest
		{
			BucketName = _bucketName,
			Key = key.Value,
			Verb = HttpVerb.GET,
			Expires = ExpiryFor(lifetime),
			Protocol = ConfiguredProtocol,
		};

		// Forces a download rather than an inline render, regardless of what the
		// browser would otherwise do with the object's content type — this is
		// what makes REQ-MED-010/011's "no inline render, ever" true at the
		// storage layer rather than relying on every caller to remember it.
		request.ResponseHeaderOverrides.ContentDisposition = AttachmentDisposition.For(downloadFileName);

		var url = await _signer.GetPreSignedURLAsync(request).ConfigureAwait(false);

		return new Uri(url);
	}

	/// <inheritdoc />
	public async Task<Stream> OpenRead(BlobKey key,
									   CancellationToken cancellationToken)
	{
		var response = await _s3.GetObjectAsync(_bucketName, key.Value, cancellationToken).ConfigureAwait(false);
		return response.ResponseStream;
	}

	/// <inheritdoc />
	public async Task Write(BlobKey key,
							Stream content,
							string contentType,
							CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		await _s3.PutObjectAsync(
			new PutObjectRequest
			{
				BucketName = _bucketName,
				Key = key.Value,
				InputStream = content,
				ContentType = contentType,
				AutoCloseStream = false,
			},
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> Exists(BlobKey key,
								   CancellationToken cancellationToken)
	{
		try
		{
			await _s3.GetObjectMetadataAsync(_bucketName, key.Value, cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (AmazonS3Exception missing) when (missing.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	/// <inheritdoc />
	public async Task Delete(BlobKey key,
							 CancellationToken cancellationToken)
	{
		// The one physical delete this system has, and it is fenced to the one
		// compartment it is for: a reporter withdrawing an upload nobody has
		// claimed. A report's own media is never erased (AGENTS.md invariant 8).
		if (key.Compartment is not MediaCompartment.Quarantine)
		{
			throw new DomainRuleViolationException("Only an unclaimed upload in quarantine may be deleted.");
		}

		// On a versioned bucket a plain DeleteObject only writes a delete marker
		// and leaves the bytes as a noncurrent version for the lifecycle rule to
		// find a day later. Deleting each version by id is what makes "removed"
		// mean gone. A prefix listing can also return longer keys that merely
		// start with this one, so only exact matches are deleted.
		var versions = new List<KeyVersion>();
		string? keyMarker = null;
		string? versionMarker = null;

		do
		{
			var page = await _s3.ListVersionsAsync(
				new ListVersionsRequest
				{
					BucketName = _bucketName,
					Prefix = key.Value,
					KeyMarker = keyMarker,
					VersionIdMarker = versionMarker,
				},
				cancellationToken).ConfigureAwait(false);

			versions.AddRange(
				(page.Versions ?? [])
				.Where(version => string.Equals(version.Key, key.Value, StringComparison.Ordinal))
				.Select(version => new KeyVersion { Key = version.Key, VersionId = version.VersionId }));

			keyMarker = page.NextKeyMarker;
			versionMarker = page.NextVersionIdMarker;

			if (page.IsTruncated != true)
			{
				break;
			}
		}
		while (true);

		if (versions.Count == 0)
		{
			return;
		}

		await _s3.DeleteObjectsAsync(
			new DeleteObjectsRequest
			{
				BucketName = _bucketName,
				Objects = versions,
				Quiet = true,
			},
			cancellationToken).ConfigureAwait(false);
	}

	// GetPreSignedUrlRequest.Expires is a local DateTime, and the SDK converts it
	// to UTC itself. BlobUrlLifetime.Validate is what stops a caller asking for a
	// URL that outlives the review session it was issued for.
	private DateTime ExpiryFor(TimeSpan lifetime)
	{
		return _clock.GetUtcNow().Add(BlobUrlLifetime.Validate(lifetime)).UtcDateTime;
	}
}
