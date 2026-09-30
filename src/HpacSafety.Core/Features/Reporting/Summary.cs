namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     A report's anonymized bilingual summary: one row per report, holding an
///     append-only list of <see cref="SummaryRevision" />s (ADR-0177). The
///     Worker's single model call writes revision 1; a reviewer's edit or rollback
///     adds the next. Nothing here rewrites a saved revision. See product
///     invariant #6 and <c>docs/data-and-persistence.md</c>.
/// </summary>
public class Summary
{
	private readonly List<SummaryRevision> _revisions = [];

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

	private Summary(TinyId reportId)
	{
		Id = TinyId.New();
		ReportId = reportId;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The report summarized. Unique: exactly one summary per report.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>When this summary was deleted along with its report, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Every revision, oldest first. Never shortened: a rollback adds one.</summary>
	public IReadOnlyList<SummaryRevision> Revisions => [.. _revisions.OrderBy(revision => revision.Sequence)];

	/// <summary>The most recent revision — the pair a reviewer sees and edits from.</summary>
	public SummaryRevision Latest => _revisions.MaxBy(revision => revision.Sequence)!;

	/// <summary>
	///     The most recent approved revision, or null when none is. This is what the
	///     public reads of a Published report, through the same rule the
	///     <c>latest_approved_summary_revisions</c> view holds (ADR-0177).
	/// </summary>
	public SummaryRevision? LatestApproved =>
		_revisions.Where(revision => revision.IsApproved).MaxBy(revision => revision.Sequence);

	/// <summary>The latest revision's English text.</summary>
	public string AiSummaryEn => Latest.AiSummaryEn;

	/// <summary>The latest revision's French text.</summary>
	public string AiSummaryFr => Latest.AiSummaryFr;

	/// <summary>How the latest revision's English text was produced (ADR-0108).</summary>
	public SummaryTextSource SourceEn => Latest.SourceEn;

	/// <summary>How the latest revision's French text was produced (ADR-0108).</summary>
	public SummaryTextSource SourceFr => Latest.SourceFr;

	/// <summary>The model the summary descends from, stamped so published text traces back.</summary>
	public string Model => Latest.Model;

	/// <summary>The prompt version the summary descends from.</summary>
	public string PromptVersion => Latest.PromptVersion;

	/// <summary>The reviewer who approved the latest revision, as an opaque token subject.</summary>
	public string? ApprovedBySubject => Latest.ApprovedBySubject;

	/// <summary>When the latest revision was approved.</summary>
	public DateTimeOffset? ApprovedAt => Latest.ApprovedAt;

	/// <summary>When the first revision was written.</summary>
	public DateTimeOffset GeneratedAt => _revisions.MinBy(revision => revision.Sequence)!.CreatedAt;

	/// <summary>When the latest revision was saved.</summary>
	public DateTimeOffset UpdatedAt => Latest.CreatedAt;

	/// <summary>True once a reviewer has approved the latest revision.</summary>
	public bool IsApproved => Latest.IsApproved;

	/// <summary>Creates the summary generated from one Worker call: revision 1, no author.</summary>
	public static Summary Generate(
		TinyId reportId,
		string aiSummaryEn,
		string aiSummaryFr,
		string model,
		string promptVersion,
		DateTimeOffset at)
	{
		return Start(reportId, aiSummaryEn, aiSummaryFr, SummaryTextSource.Generated, SummaryTextSource.Generated, model, promptVersion, null, at);
	}

	/// <summary>
	///     Creates the summary a reviewer wrote by hand after summarization failed:
	///     revision 1, authored by that reviewer (REQ-MOD-059).
	/// </summary>
	internal static Summary Write(
		TinyId reportId,
		string aiSummaryEn,
		string aiSummaryFr,
		SummaryTextSource sourceEn,
		SummaryTextSource sourceFr,
		string model,
		string promptVersion,
		string authorSubject,
		DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(authorSubject);

		return Start(reportId, aiSummaryEn, aiSummaryFr, ReviewerSource(sourceEn), ReviewerSource(sourceFr), model, promptVersion, authorSubject, at);
	}

	/// <summary>
	///     Adds a revision holding both languages. A language whose text did not
	///     change keeps how it was produced; a changed one records the given source
	///     (ADR-0108).
	/// </summary>
	internal SummaryRevision Edit(string textEn,
								  string textFr,
								  string authorSubject,
								  DateTimeOffset at,
								  SummaryTextSource sourceEn,
								  SummaryTextSource sourceFr)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(authorSubject);

		var latest = Latest;
		var enChanged = !string.Equals(textEn, latest.AiSummaryEn, StringComparison.Ordinal);
		var frChanged = !string.Equals(textFr, latest.AiSummaryFr, StringComparison.Ordinal);

		if (!enChanged && !frChanged)
		{
			throw new DomainRuleViolationException("Nothing changed. A revision holds a change to at least one language.");
		}

		return Append(new SummaryRevision(
			Id,
			latest.Sequence + 1,
			textEn,
			textFr,
			enChanged ? ReviewerSource(sourceEn) : latest.SourceEn,
			frChanged ? ReviewerSource(sourceFr) : latest.SourceFr,
			latest.Model,
			latest.PromptVersion,
			authorSubject,
			at,
			null));
	}

	/// <summary>
	///     Adds a revision that copies an earlier one's text and sources, and names
	///     it. Versions only move forward: the earlier revision is untouched.
	/// </summary>
	internal SummaryRevision Restore(TinyId revisionId,
									 string authorSubject,
									 DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(authorSubject);

		var earlier = _revisions.SingleOrDefault(revision => revision.Id == revisionId)
					  ?? throw new DomainRuleViolationException("That revision does not belong to this summary.");

		if (earlier.Sequence == Latest.Sequence)
		{
			throw new DomainRuleViolationException("That revision is already the current one.");
		}

		return Append(new SummaryRevision(
			Id,
			Latest.Sequence + 1,
			earlier.AiSummaryEn,
			earlier.AiSummaryFr,
			earlier.SourceEn,
			earlier.SourceFr,
			earlier.Model,
			earlier.PromptVersion,
			authorSubject,
			at,
			earlier.Id));
	}

	/// <summary>Records a reviewer's approval of the latest revision, both languages together.</summary>
	public void Approve(string approverSubject,
						DateTimeOffset at)
	{
		Latest.Approve(approverSubject, at);
	}

	/// <summary>Withdraws the latest revision's approval, as unpublishing does (REQ-MOD-057).</summary>
	internal void ClearApproval()
	{
		Latest.ClearApproval();
	}

	/// <summary>Stamps this summary and its revisions deleted, as part of its report's soft deletion (REQ-DOM-007).</summary>
	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;

		foreach (var revision in _revisions)
		{
			revision.Delete(at);
		}
	}

	/// <summary>A reviewer's text is theirs or an accepted translation — never "generated".</summary>
	internal static SummaryTextSource ReviewerSource(SummaryTextSource source)
	{
		return source is SummaryTextSource.Human or SummaryTextSource.Machine
			? source
			: throw new DomainRuleViolationException("A reviewer's text is written by hand or machine-translated, never generated.");
	}

	private static Summary Start(TinyId reportId,
								 string textEn,
								 string textFr,
								 SummaryTextSource sourceEn,
								 SummaryTextSource sourceFr,
								 string model,
								 string promptVersion,
								 string? authorSubject,
								 DateTimeOffset at)
	{
		var summary = new Summary(reportId);
		summary._revisions.Add(new SummaryRevision(summary.Id, 1, textEn, textFr, sourceEn, sourceFr, model, promptVersion, authorSubject, at, null));
		return summary;
	}

	private SummaryRevision Append(SummaryRevision revision)
	{
		_revisions.Add(revision);
		return revision;
	}
}
