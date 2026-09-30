namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     One saved version of a report's bilingual summary pair (ADR-0177). A
///     revision is written once and never rewritten: an edit or a rollback adds a
///     new revision after it. The only fields that ever change on a stored row are
///     its approval (set when a reviewer approves it, cleared when the report is
///     unpublished) and its deletion stamp when the report is deleted.
/// </summary>
public class SummaryRevision
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private SummaryRevision()
	{
	}
#pragma warning restore CS8618

	internal SummaryRevision(TinyId summaryId,
							 int sequence,
							 string aiSummaryEn,
							 string aiSummaryFr,
							 SummaryTextSource sourceEn,
							 SummaryTextSource sourceFr,
							 string model,
							 string promptVersion,
							 string? authorSubject,
							 DateTimeOffset createdAt,
							 TinyId? restoredFromId)
	{
		Id = TinyId.New();
		SummaryId = summaryId;
		Sequence = sequence;
		AiSummaryEn = NotBlank(aiSummaryEn);
		AiSummaryFr = NotBlank(aiSummaryFr);
		SourceEn = sourceEn;
		SourceFr = sourceFr;
		Model = model;
		PromptVersion = promptVersion;
		AuthorSubject = authorSubject;
		CreatedAt = createdAt;
		RestoredFromId = restoredFromId;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The summary this is a revision of.</summary>
	public TinyId SummaryId { get; private init; }

	/// <summary>1 for the first revision, then one more for each revision after it.</summary>
	public int Sequence { get; private init; }

	/// <summary>The English text.</summary>
	public string AiSummaryEn { get; private init; }

	/// <summary>The French text.</summary>
	public string AiSummaryFr { get; private init; }

	/// <summary>How the English text was produced (ADR-0108).</summary>
	public SummaryTextSource SourceEn { get; private init; }

	/// <summary>How the French text was produced (ADR-0108).</summary>
	public SummaryTextSource SourceFr { get; private init; }

	/// <summary>The model behind the text this revision descends from, so published text traces back.</summary>
	public string Model { get; private init; }

	/// <summary>The prompt version behind the text this revision descends from.</summary>
	public string PromptVersion { get; private init; }

	/// <summary>
	///     Who saved it, as the subject claim of their validated token. Opaque, and
	///     deliberately not a key (ADR-0065). Null for the Worker's revision, and for
	///     a revision written before authors were recorded.
	/// </summary>
	public string? AuthorSubject { get; private init; }

	/// <summary>When it was saved.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>The earlier revision this one restores, when it is a rollback.</summary>
	public TinyId? RestoredFromId { get; private init; }

	/// <summary>The reviewer who approved this revision, as an opaque token subject.</summary>
	public string? ApprovedBySubject { get; private set; }

	/// <summary>When this revision was approved.</summary>
	public DateTimeOffset? ApprovedAt { get; private set; }

	/// <summary>When this revision was deleted along with its report, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>True once a reviewer has approved this revision.</summary>
	public bool IsApproved => ApprovedAt is not null;

	internal void Approve(string approverSubject,
						  DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(approverSubject);
		ApprovedBySubject = approverSubject;
		ApprovedAt = at;
	}

	internal void ClearApproval()
	{
		ApprovedBySubject = null;
		ApprovedAt = null;
	}

	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}

	private static string NotBlank(string text)
	{
		return string.IsNullOrWhiteSpace(text)
			? throw new DomainRuleViolationException("A summary cannot be blank.")
			: text;
	}
}
