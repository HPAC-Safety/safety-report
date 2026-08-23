using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Infrastructure.Storage;

/// <summary>
/// An <b>Adapter</b> (Gang of Four) over the AWS S3 SDK, which is why
/// <c>HpacSafety.Core</c> can talk about private object storage without knowing
/// AWS exists. It is S3-compatible rather than AWS-specific, so the same class
/// serves S3, Cloudflare R2, and the MinIO container the contract suite runs
/// against. See ADR-0026.
/// <para>
/// A pre-signed URL is signed over the bucket, the key, the verb, and the
/// expiry. Point it at a different key and the signature no longer matches, so
/// S3 answers <c>403</c> — the URL is a capability for one object, not a
/// password for the bucket.
/// </para>
/// </summary>
public sealed class S3BlobStore : IBlobStore
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucketName;
    private readonly TimeProvider _clock;

    /// <summary>Creates the adapter.</summary>
    public S3BlobStore(IAmazonS3 s3, S3BlobStoreOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(s3);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BucketName);

        _s3 = s3;
        _bucketName = options.BucketName;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Uri> CreateUploadUrlAsync(BlobKey key, string contentType, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var url = await _s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key.Value,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = ExpiryFor(lifetime),
            Protocol = ConfiguredProtocol,
        }).ConfigureAwait(false);

        return new Uri(url);
    }

    /// <inheritdoc />
    public async Task<Uri> CreateReadUrlAsync(BlobKey key, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var url = await _s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key.Value,
            Verb = HttpVerb.GET,
            Expires = ExpiryFor(lifetime),
            Protocol = ConfiguredProtocol,
        }).ConfigureAwait(false);

        return new Uri(url);
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(BlobKey key, CancellationToken cancellationToken)
    {
        var response = await _s3.GetObjectAsync(_bucketName, key.Value, cancellationToken).ConfigureAwait(false);
        return response.ResponseStream;
    }

    /// <inheritdoc />
    public async Task WriteAsync(BlobKey key, Stream content, string contentType, CancellationToken cancellationToken)
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

    // The SDK defaults a pre-signed URL to HTTPS regardless of the configured
    // endpoint, which is right for S3 and wrong for an S3-compatible server
    // reached over plain HTTP - MinIO in development and in the contract suite.
    // The scheme is not part of what SigV4 signs, so this decides where the URL
    // points, never whether it is valid. Production has no ServiceURL set and
    // therefore stays on HTTPS.
    private Protocol ConfiguredProtocol =>
        _s3.Config.ServiceURL?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? Protocol.HTTP
            : Protocol.HTTPS;

    // GetPreSignedUrlRequest.Expires is a local DateTime, and the SDK converts it
    // to UTC itself. BlobUrlLifetime.Validate is what stops a caller asking for a
    // URL that outlives the review session it was issued for.
    private DateTime ExpiryFor(TimeSpan lifetime) =>
        _clock.GetUtcNow().Add(BlobUrlLifetime.Validate(lifetime)).UtcDateTime;
}
