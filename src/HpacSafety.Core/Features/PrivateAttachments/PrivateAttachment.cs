using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Features.PrivateAttachments;

/// <summary>
///     A file a safety officer or administrator added to a report for staff only
///     (ADR-0135): a coroner's report, a police report, an investigation archive.
///     Not a reporter attachment: it is never summarized, sent to the model,
///     processed by the Worker, counted, or published. Its bytes are stored under
///     the report's private compartment exactly as uploaded. Nothing is ever
///     erased: removing one soft-deletes it.
/// </summary>
public class PrivateAttachment
{
	/// <summary>The longest description staff may write.</summary>
	public const int DescriptionMaxLength = 500;

	/// <summary>The longest token subject stored, the same width as every other subject column.</summary>
	public const int SubjectMaxLength = 256;

	// For EF Core only; see ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private PrivateAttachment()
	{
	}
#pragma warning restore CS8618

	private PrivateAttachment(TinyId id,
							  TinyId reportId,
							  BlobKey blobKey,
							  string originalFileName,
							  string contentType,
							  long byteSize,
							  string? description,
							  string addedBySubject,
							  DateTimeOffset at)
	{
		Id = id;
		ReportId = reportId;
		BlobKey = blobKey.Value;
		OriginalFileName = originalFileName;
		ContentType = contentType;
		ByteSize = byteSize;
		Description = description;
		AddedBySubject = addedBySubject;
		AddedAt = at;
	}

	/// <summary>Surrogate key, and the last segment of its blob key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The report it belongs to.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>Where its bytes are: <c>&lt;report id&gt;/private/&lt;id&gt;</c>.</summary>
	public string BlobKey { get; private init; }

	/// <summary>The adder's file name, sanitized (ADR-0097); the name its download saves as.</summary>
	public string OriginalFileName { get; private init; }

	/// <summary>The type the upload was signed and stored as. Never sniffed.</summary>
	public string ContentType { get; private init; }

	/// <summary>The stored size in bytes.</summary>
	public long ByteSize { get; private init; }

	/// <summary>Optional plain text the adder wrote about it.</summary>
	public string? Description { get; private init; }

	/// <summary>
	///     Who added it, as their token's subject. Opaque, and not a key: there is no
	///     user table (ADR-0065).
	/// </summary>
	public string AddedBySubject { get; private init; }

	/// <summary>When it was added.</summary>
	public DateTimeOffset AddedAt { get; private init; }

	/// <summary>When it was removed, or its report deleted, if either happened.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Who removed it, or deleted its report, as a token subject.</summary>
	public string? DeletedBySubject { get; private set; }

	/// <summary>
	///     Records a file already copied into <paramref name="reportId" />'s private
	///     compartment under <paramref name="id" />.
	/// </summary>
	/// <exception cref="DomainRuleViolationException">
	///     The key is not that report's private key for this id, the name sanitizes
	///     to nothing, the size is not positive, or the description is too long.
	/// </exception>
	public static PrivateAttachment Add(TinyId id,
										TinyId reportId,
										BlobKey blobKey,
										string? fileName,
										string contentType,
										long byteSize,
										string? description,
										string addedBySubject,
										DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
		ArgumentException.ThrowIfNullOrWhiteSpace(addedBySubject);

		if (blobKey.Compartment is not MediaCompartment.Private
			|| blobKey.ReportId != reportId.Value
			|| blobKey.FileName != id.Value)
		{
			throw new DomainRuleViolationException("A private attachment lives in its own report's private compartment, named by its id.");
		}

		var name = AttachmentFileName.Sanitize(fileName)
				   ?? throw new DomainRuleViolationException("A private attachment needs a file name.");

		if (byteSize <= 0)
		{
			throw new DomainRuleViolationException("A private attachment holds at least one byte.");
		}

		var trimmed = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

		if (trimmed is { Length: > DescriptionMaxLength })
		{
			throw new DomainRuleViolationException($"A private attachment's description is at most {DescriptionMaxLength} characters.");
		}

		return new PrivateAttachment(id, reportId, blobKey, name, contentType, byteSize, trimmed, addedBySubject, at);
	}

	/// <summary>
	///     Soft-deletes it, recording who did. Its bytes are kept (AGENTS.md
	///     invariant 8). Removing it twice keeps the first time. There is no undo.
	/// </summary>
	public void Remove(string removedBySubject,
					   DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(removedBySubject);

		if (Deleted is not null)
		{
			return;
		}

		Deleted = at;
		DeletedBySubject = removedBySubject;
	}
}
