namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The only sanctioned way to hand a browser somewhere to send a file: a fresh
///     opaque upload id and a short-lived pre-signed PUT to
///     <c>quarantine/&lt;upload id&gt;</c>, signed for the declared type and exact
///     size (ADR-0126). A reporter's attachment is minted by <see cref="Mint" />; a
///     staff private attachment by <see cref="MintPrivate" />, to the same
///     quarantine (ADR-0135).
///     <para>
///         The declaration is judged first, and a refused one mints nothing — no id,
///         no URL, no object, no row. An accepted one mints a URL and still writes
///         nothing: the bytes arrive later, straight from the browser to storage, and
///         are sniffed and validated when a submission claims them.
///     </para>
///     <para>
///         Nothing here knows who asked. The URL is signed with the service's own
///         role, and the upload id is the only handle anybody has on the file
///         (ADR-0067).
///     </para>
/// </summary>
public sealed class UploadLink
{
	private readonly IBlobStore _blobStore;
	private readonly TimeProvider _clock;
	private readonly MediaPolicy _policy;

	/// <summary>Creates the link issuer.</summary>
	public UploadLink(IBlobStore blobStore,
					  MediaPolicy policy,
					  TimeProvider clock)
	{
		ArgumentNullException.ThrowIfNull(blobStore);
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(clock);

		_blobStore = blobStore;
		_policy = policy;
		_clock = clock;
	}

	/// <summary>
	///     Mints an upload for a file the browser declares, or says why it will not.
	/// </summary>
	/// <param name="declaredContentType">The type the browser read from the file.</param>
	/// <param name="byteSize">The file's exact size.</param>
	/// <param name="cancellationToken">Cancels the signing.</param>
	public async Task<UploadLinkResult> Mint(string? declaredContentType,
											 long byteSize,
											 CancellationToken cancellationToken)
	{
		var verdict = _policy.JudgeDeclaration(declaredContentType, byteSize);
		if (!verdict.IsAccepted)
		{
			return UploadLinkResult.Refused(verdict.RejectionReason);
		}

		var uploadId = UploadId.New();
		var lifetime = BlobUrlLifetime.Maximum;

		// Signed for the declaration's essence as the browser gave it — lower
		// case, parameters dropped — so the browser sends back exactly the type it
		// declared. The claim compares that type with what the bytes really are.
		var contentType = Essence(declaredContentType!);
		var url = await _blobStore
			.CreateUploadUrl(BlobKey.ForUpload(uploadId), contentType, byteSize, lifetime, cancellationToken)
			.ConfigureAwait(false);

		return UploadLinkResult.Minted(uploadId, verdict.Type.Kind, url, _clock.GetUtcNow().Add(lifetime));
	}

	/// <summary>
	///     Mints a staff private upload (ADR-0135), or says why it will not. Judged on
	///     size alone against <paramref name="policy" />: any type is accepted, signed
	///     as <see cref="PrivateAttachments.PrivateAttachmentPolicy.ContentTypeFor" />
	///     gives it. The PUT lands in quarantine exactly as a reporter's does, so the
	///     same lifecycle rule expires it unless a staff request claims it.
	/// </summary>
	/// <param name="policy">The private attachment cap.</param>
	/// <param name="declaredContentType">The type the browser read from the file, if any.</param>
	/// <param name="byteSize">The file's exact size.</param>
	/// <param name="cancellationToken">Cancels the signing.</param>
	public async Task<PrivateUploadLinkResult> MintPrivate(PrivateAttachments.PrivateAttachmentPolicy policy,
														   string? declaredContentType,
														   long byteSize,
														   CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(policy);

		var refusal = policy.JudgeSize(byteSize);
		if (refusal is not MediaRejectionReason.None)
		{
			return PrivateUploadLinkResult.Refused(refusal);
		}

		var uploadId = UploadId.New();
		var lifetime = BlobUrlLifetime.Maximum;
		var contentType = PrivateAttachments.PrivateAttachmentPolicy.ContentTypeFor(declaredContentType);
		var url = await _blobStore
			.CreateUploadUrl(BlobKey.ForUpload(uploadId), contentType, byteSize, lifetime, cancellationToken)
			.ConfigureAwait(false);

		return PrivateUploadLinkResult.Minted(uploadId, contentType, url, _clock.GetUtcNow().Add(lifetime));
	}

	/// <summary>A declared type without parameters, as one canonical string.</summary>
	public static string Essence(string declaredContentType)
	{
		ArgumentNullException.ThrowIfNull(declaredContentType);

		return declaredContentType.Split(';')[0].Trim().ToLowerInvariant();
	}
}

/// <summary>What <see cref="UploadLink.Mint" /> decided.</summary>
public sealed record UploadLinkResult
{
	private UploadLinkResult(MediaRejectionReason rejectionReason,
							 UploadId uploadId,
							 MediaKind kind,
							 Uri? url,
							 DateTimeOffset expiresAt)
	{
		RejectionReason = rejectionReason;
		UploadId = uploadId;
		Kind = kind;
		Url = url;
		ExpiresAt = expiresAt;
	}

	/// <summary>Whether an upload was minted.</summary>
	public bool IsMinted => Url is not null;

	/// <summary>Why nothing was minted, or <see cref="MediaRejectionReason.None" />.</summary>
	public MediaRejectionReason RejectionReason { get; }

	/// <summary>The minted upload's id.</summary>
	public UploadId UploadId { get; }

	/// <summary>The declared kind the size was judged against.</summary>
	public MediaKind Kind { get; }

	/// <summary>The pre-signed PUT.</summary>
	public Uri? Url { get; }

	/// <summary>When <see cref="Url" /> stops working.</summary>
	public DateTimeOffset ExpiresAt { get; }

	/// <summary>An upload was minted.</summary>
	public static UploadLinkResult Minted(UploadId uploadId,
										  MediaKind kind,
										  Uri url,
										  DateTimeOffset expiresAt)
	{
		ArgumentNullException.ThrowIfNull(url);

		return new UploadLinkResult(MediaRejectionReason.None, uploadId, kind, url, expiresAt);
	}

	/// <summary>The declaration was refused and nothing was minted.</summary>
	public static UploadLinkResult Refused(MediaRejectionReason reason)
	{
		return new UploadLinkResult(reason, default, default, null, default);
	}
}

/// <summary>What <see cref="UploadLink.MintPrivate" /> decided.</summary>
public sealed record PrivateUploadLinkResult
{
	private PrivateUploadLinkResult(MediaRejectionReason rejectionReason,
									UploadId uploadId,
									string? contentType,
									Uri? url,
									DateTimeOffset expiresAt)
	{
		RejectionReason = rejectionReason;
		UploadId = uploadId;
		ContentType = contentType;
		Url = url;
		ExpiresAt = expiresAt;
	}

	/// <summary>Whether an upload was minted.</summary>
	public bool IsMinted => Url is not null;

	/// <summary>Why nothing was minted, or <see cref="MediaRejectionReason.None" />.</summary>
	public MediaRejectionReason RejectionReason { get; }

	/// <summary>The minted upload's id.</summary>
	public UploadId UploadId { get; }

	/// <summary>The type the PUT is signed for, which the browser must send.</summary>
	public string? ContentType { get; }

	/// <summary>The pre-signed PUT.</summary>
	public Uri? Url { get; }

	/// <summary>When <see cref="Url" /> stops working.</summary>
	public DateTimeOffset ExpiresAt { get; }

	/// <summary>An upload was minted.</summary>
	public static PrivateUploadLinkResult Minted(UploadId uploadId,
												 string contentType,
												 Uri url,
												 DateTimeOffset expiresAt)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
		ArgumentNullException.ThrowIfNull(url);

		return new PrivateUploadLinkResult(MediaRejectionReason.None, uploadId, contentType, url, expiresAt);
	}

	/// <summary>The declaration was refused and nothing was minted.</summary>
	public static PrivateUploadLinkResult Refused(MediaRejectionReason reason)
	{
		return new PrivateUploadLinkResult(reason, default, null, null, default);
	}
}
