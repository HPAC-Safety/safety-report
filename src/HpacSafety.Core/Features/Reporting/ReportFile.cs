namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     An uploaded attachment. The original bytes stay private; for an image or
///     video, the EXIF-stripped derivative is what a reviewer sees, and what a
///     published report shows when its reporter consented to media, unless a
///     reviewer hid it (ADR-0117). A document has no derivative at all —
///     it is validated and kept private; there is no malware scan (ADR-0089). See
///     docs/data-handling.md.
/// </summary>
public class ReportFile
{
	/// <summary>Records an upload that has landed in the private bucket.</summary>
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else — domain code still has to go through the
	// constructor or factory that follows, so no caller can reach a half-built
	// aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private ReportFile()
	{
	}
#pragma warning restore CS8618

	public ReportFile(TinyId reportId,
					  string blobKey,
					  string contentType,
					  long byteSize,
					  DateTimeOffset uploadedAt)
		: this(TinyId.New(), reportId, blobKey, contentType, byteSize, originalFileName: null, uploadedAt)
	{
	}

	/// <summary>
	///     Records a claimed upload under the id its blobs are already named by, with
	///     the reporter's filename (ADR-0097). The name is sanitized here, so no
	///     caller can store one that was not.
	/// </summary>
	public ReportFile(TinyId id,
					  TinyId reportId,
					  string blobKey,
					  string contentType,
					  long byteSize,
					  string? originalFileName,
					  DateTimeOffset uploadedAt)
	{
		if (id.IsEmpty)
		{
			throw new DomainRuleViolationException("A report file needs an id.");
		}

		Id = id;
		OriginalFileName = AttachmentFileName.Sanitize(originalFileName);
		ReportId = reportId;
		BlobKey = blobKey;
		ContentType = contentType;
		Kind = MediaType.TryParse(contentType, out var mediaType)
			? mediaType.Kind switch
			{
				MediaKind.Video => AttachmentKind.Video,
				MediaKind.Document => AttachmentKind.Document,
				_ => AttachmentKind.Image,
			}
			: AttachmentKind.Document;
		ByteSize = byteSize;
		UploadedAt = uploadedAt;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The report this file belongs to.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>
	///     The file-upload answer this attachment belongs to, once it is linked.
	///     Every attachment belongs to exactly one file-upload answer on the same
	///     report — the answer identifies the exact question revision asked.
	/// </summary>
	public TinyId? ReportAnswerId { get; private set; }

	/// <summary>Whether this is an image, a video, or a private document.</summary>
	public AttachmentKind Kind { get; private init; }

	/// <summary>Key of the private original bytes.</summary>
	public string BlobKey { get; private init; }

	/// <summary>Key of the EXIF-stripped derivative a reviewer is shown. Documents never have one.</summary>
	public string? StrippedBlobKey { get; private set; }

	/// <summary>
	///     The reporter's own name for the file, sanitized, or <see langword="null" />
	///     when none was given or it sanitized to nothing. Used only as a reviewer's
	///     download name — never logged, never in a key, never sent to the model or a
	///     public DTO (ADR-0097).
	/// </summary>
	public string? OriginalFileName { get; private init; }

	/// <summary>Content type as sniffed on ingest, never as the client claimed.</summary>
	public string ContentType { get; private init; }

	/// <summary>Size in bytes.</summary>
	public long ByteSize { get; private init; }

	/// <summary>When it was uploaded.</summary>
	public DateTimeOffset UploadedAt { get; private init; }

	/// <summary>When EXIF — GPS above all — was stripped.</summary>
	public DateTimeOffset? ExifStrippedAt { get; private set; }

	/// <summary>A safe, non-content error code recorded when processing this file failed.</summary>
	public string? ProcessingErrorCode { get; private set; }

	/// <summary>
	///     When a reviewer hid this file from the published report, if one did and
	///     has not shown it again. A hide never touches the bytes (ADR-0117).
	/// </summary>
	public DateTimeOffset? HiddenAt { get; private set; }

	/// <summary>The opaque token subject of the reviewer who hid it — never shown publicly.</summary>
	public string? HiddenBySubject { get; private set; }

	/// <summary>When this file was deleted along with its report, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>
	///     True until a stripped derivative exists. A file is not viewable before
	///     then — and a video has no derivative at all yet, so it stays true. See
	///     issue #65.
	///     <para>
	///         Both fields are checked, not just the timestamp: a row carrying a
	///         stripped-at time with no key would otherwise read as viewable.
	///     </para>
	/// </summary>
	public bool AwaitsStripping => ExifStrippedAt is null || StrippedBlobKey is null;

	/// <summary>
	///     The key of the only bytes a reviewer may be shown.
	///     <para>
	///         Reading this while <see cref="AwaitsStripping" /> throws rather than
	///         returning <see cref="BlobKey" />. Falling back to the original is the
	///         leak this whole feature exists to prevent, and a caller that asks for
	///         something to show when there is nothing safe to show has a bug worth
	///         failing loudly. It is the persisted counterpart of
	///         <see cref="MediaIngestOutcome.DerivativeKey" />.
	///     </para>
	/// </summary>
	// Qualified, because this entity has a string property named BlobKey that
	// shadows the type of the same name.
	public BlobKey ViewableKey =>
		AwaitsStripping
			? throw new DomainRuleViolationException("There is no stripped derivative for a reviewer to see.")
			: Core.BlobKey.Parse(StrippedBlobKey);

	/// <summary>Records the stripped derivative. Both facts are recorded together or not at all.</summary>
	public void RecordStripped(string strippedBlobKey,
							   DateTimeOffset at)
	{
		var parsed = Core.BlobKey.Parse(strippedBlobKey);

		if (parsed.Compartment is not MediaCompartment.Stripped)
		{
			throw new DomainRuleViolationException("A derivative must live in the stripped compartment.");
		}

		StrippedBlobKey = parsed.Value;
		ExifStrippedAt = at;
	}

	/// <summary>Links this attachment to the file-upload answer it was submitted with.</summary>
	public void LinkToAnswer(TinyId reportAnswerId)
	{
		ReportAnswerId = reportAnswerId;
	}

	/// <summary>
	///     A reviewer hides this image or video from the published report. Hiding a
	///     file already hidden changes nothing, so the first hide's record stands.
	/// </summary>
	/// <returns>True when this call hid it.</returns>
	public bool HideBy(string subject,
					   DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(subject);
		EnsureMedia();

		if (HiddenAt is not null)
		{
			return false;
		}

		HiddenAt = at;
		HiddenBySubject = subject;
		return true;
	}

	/// <summary>A reviewer shows a hidden image or video again.</summary>
	/// <returns>True when this call showed it.</returns>
	public bool Show()
	{
		EnsureMedia();

		if (HiddenAt is null)
		{
			return false;
		}

		HiddenAt = null;
		HiddenBySubject = null;
		return true;
	}

	private void EnsureMedia()
	{
		if (Deleted is not null)
		{
			throw new DomainRuleViolationException("This file was deleted with its report.");
		}

		if (Kind is not (AttachmentKind.Image or AttachmentKind.Video))
		{
			throw new DomainRuleViolationException("Only an image or a video is ever public, so only one can be hidden or shown.");
		}
	}

	/// <summary>Records that processing this file failed, with a safe non-content code.</summary>
	public void RecordProcessingFailure(string errorCode)
	{
		ProcessingErrorCode = errorCode;
	}

	/// <summary>Stamps this file deleted, as part of its report's soft deletion (REQ-DOM-007).</summary>
	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}
}
