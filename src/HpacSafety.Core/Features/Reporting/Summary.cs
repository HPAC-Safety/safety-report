namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     The anonymized bilingual summary of a report. Exactly one row per report: the
///     Worker's single model call produces both languages together, so there is
///     nothing to keep in sync between them, and one shared approval covers the
///     pair. See product invariant #6 and <c>docs/data-and-persistence.md</c>.
/// </summary>
public class Summary
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else — domain code still has to go through the
	// constructor or factory that follows, so no caller can reach a half-built
	// aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private Summary()
	{
	}
#pragma warning restore CS8618

	private Summary(TinyId reportId,
					string aiSummaryEn,
					string aiSummaryFr,
					string model,
					string promptVersion,
					DateTimeOffset at)
	{
		Id = TinyId.New();
		ReportId = reportId;
		AiSummaryEn = NotBlank(aiSummaryEn);
		AiSummaryFr = NotBlank(aiSummaryFr);
		Model = model;
		PromptVersion = promptVersion;
		GeneratedAt = at;
		UpdatedAt = at;
		SourceEn = SummaryTextSource.Generated;
		SourceFr = SummaryTextSource.Generated;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The report summarized. Unique: exactly one summary per report.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>How the English text was produced (ADR-0106).</summary>
	public SummaryTextSource SourceEn { get; private set; }

	/// <summary>How the French text was produced (ADR-0106).</summary>
	public SummaryTextSource SourceFr { get; private set; }

	/// <summary>The English text. Publishable only once the pair is approved.</summary>
	public string AiSummaryEn { get; private set; }

	/// <summary>The French text. Publishable only once the pair is approved.</summary>
	public string AiSummaryFr { get; private set; }

	/// <summary>The model that produced it, stamped so published text traces back.</summary>
	public string Model { get; private init; }

	/// <summary>The prompt version that produced it.</summary>
	public string PromptVersion { get; private init; }

	/// <summary>
	///     The safety officer who approved the pair, as the subject claim of their
	///     validated token. Opaque, and deliberately not a key — there is no user
	///     table to join to (ADR-0065).
	/// </summary>
	public string? ApprovedBySubject { get; private set; }

	/// <summary>When the pair was approved.</summary>
	public DateTimeOffset? ApprovedAt { get; private set; }

	/// <summary>When it was generated.</summary>
	public DateTimeOffset GeneratedAt { get; private init; }

	/// <summary>When either language last changed.</summary>
	public DateTimeOffset UpdatedAt { get; private set; }

	/// <summary>When this summary was deleted along with its report, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>True once a human has approved the pair.</summary>
	public bool IsApproved => ApprovedAt is not null;

	/// <summary>Creates the summary generated from one Worker call.</summary>
	public static Summary Generate(
		TinyId reportId,
		string aiSummaryEn,
		string aiSummaryFr,
		string model,
		string promptVersion,
		DateTimeOffset at)
	{
		return new Summary(reportId, aiSummaryEn, aiSummaryFr, model, promptVersion, at);
	}

	/// <summary>
	///     Replaces the English text by hand — the escape hatch when the model
	///     failed. Editing either language clears the pair's approval.
	/// </summary>
	public void RewriteEn(string text,
						  DateTimeOffset at,
						  SummaryTextSource source = SummaryTextSource.Human)
	{
		AiSummaryEn = NotBlank(text);
		SourceEn = ReviewerSource(source);
		UpdatedAt = at;
		ClearApproval();
	}

	/// <summary>
	///     Replaces the French text by hand. Editing either language clears the
	///     pair's approval.
	/// </summary>
	public void RewriteFr(string text,
						  DateTimeOffset at,
						  SummaryTextSource source = SummaryTextSource.Human)
	{
		AiSummaryFr = NotBlank(text);
		SourceFr = ReviewerSource(source);
		UpdatedAt = at;
		ClearApproval();
	}

	/// <summary>Records a safety officer's approval of the whole pair.</summary>
	public void Approve(string approverSubject,
						DateTimeOffset at)
	{
		ApprovedBySubject = approverSubject;
		ApprovedAt = at;
	}

	/// <summary>Withdraws the pair's approval, as unpublishing does (REQ-MOD-057).</summary>
	internal void ClearApproval()
	{
		ApprovedBySubject = null;
		ApprovedAt = null;
	}

	/// <summary>Stamps this summary deleted, as part of its report's soft deletion (REQ-DOM-007).</summary>
	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}

	/// <summary>A reviewer's text is theirs or an accepted translation — never "generated".</summary>
	internal static SummaryTextSource ReviewerSource(SummaryTextSource source)
	{
		return source is SummaryTextSource.Human or SummaryTextSource.Machine
			? source
			: throw new DomainRuleViolationException("A reviewer's text is written by hand or machine-translated, never generated.");
	}

	private static string NotBlank(string text)
	{
		return string.IsNullOrWhiteSpace(text)
			? throw new DomainRuleViolationException("A summary cannot be blank.")
			: text;
	}
}
