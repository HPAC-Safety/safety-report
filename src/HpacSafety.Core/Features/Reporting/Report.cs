using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     An occurrence report. Every ordinary answer is data — one row per question
///     asked, in <see cref="Answers" /> — and the two consents, publication and
///     media, are the only ones that additionally project onto a typed property
///     here, because they are read by logic rather than only displayed. See <c>docs/data-and-persistence.md</c>.
/// </summary>
public class Report
{
	private readonly List<ReportAnswer> _answers = [];
	private readonly List<ReportFile> _files = [];

	/// <summary>Opens a report in the language the reporter is writing in.</summary>
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property and backing field directly. It exists for
	// the ORM and for nothing else — domain code still has to go through the
	// constructor or factory that follows, so no caller can reach a half-built
	// aggregate. See ADR-0019.
	private Report()
	{
	}

	public Report(Locale language,
				  DateTimeOffset submittedAt)
	{
		Id = TinyId.New();
		Language = language;
		SubmittedAt = submittedAt;
		Status = ReportStatus.Submitted;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>
	///     The locale the report was written in. The Worker summarizes in this
	///     language and produces the other in the same call; the raw narrative is
	///     never translated.
	/// </summary>
	public Locale Language { get; private init; }

	/// <summary>Where the report is in its lifecycle.</summary>
	public ReportStatus Status { get; private set; }

	/// <summary>When it was received.</summary>
	public DateTimeOffset SubmittedAt { get; private init; }

	/// <summary>When it was published, if it was.</summary>
	public DateTimeOffset? PublishedAt { get; private set; }

	/// <summary>
	///     Whether the reporter agreed to publication of a de-identified version.
	///     <b>Null means unanswered</b>, which is a different thing from "no": the
	///     consent question is required and has no default, so a reporter must
	///     choose one before submitting. Silence is not consent, and neither is a
	///     pre-selected radio button.
	/// </summary>
	public bool? ConsentPublish { get; private set; }

	/// <summary>True once the reporter has actually chosen yes or no.</summary>
	public bool HasAnsweredConsent => ConsentPublish is not null;

	/// <summary>
	///     Whether the reporter agreed to HPAC showing their photos and video on the
	///     published report (ADR-0117). <b>Null means unanswered</b> — the form asks
	///     it only when publication consent is yes and an image or video is attached,
	///     and a report filed before the question existed never answered it. Only
	///     <see langword="true" /> lets any media be public.
	/// </summary>
	public bool? ConsentMedia { get; private set; }

	/// <summary>
	///     Why summarization failed, when it did. Attached so the report
	///     still reaches a human rather than disappearing.
	/// </summary>
	public string? SummaryError { get; private set; }

	/// <summary>
	///     A reviewer's optional note on why the report was rejected. Shown only in
	///     the admin report view — never public, audited, or logged (REQ-MOD-058).
	///     Cleared when the report is reopened.
	/// </summary>
	public string? RejectionNote { get; private set; }

	/// <summary>When this report was soft-deleted, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Every answer given, against the question revision it was given under.</summary>
	public IReadOnlyList<ReportAnswer> Answers => _answers;

	/// <summary>Uploaded attachments.</summary>
	public IReadOnlyList<ReportFile> Files => _files;

	/// <summary>The bilingual summary, once the Worker has produced one.</summary>
	public Summary? Summary { get; private set; }

	/// <summary>
	///     True only when a reporter consented, a safety officer approved the report,
	///     the summary pair was approved, and neither the report nor its summary has
	///     been soft-deleted. Every clause is load bearing: nothing reaches the
	///     public without all of them (REQ-DOM-003/004).
	/// </summary>
	public bool IsPublishable =>
		Deleted is null
		&& ConsentPublish is true
		&& Status is ReportStatus.Approved or ReportStatus.Published
		&& Summary is { IsApproved: true, Deleted: null };

	/// <summary>
	///     Records one answer against the question's current revision, in the
	///     report's own language, projecting it if the question carries a role.
	/// </summary>
	public ReportAnswer Answer(Question question,
							   string? value,
							   DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(question);

		return Answer(question, question.CurrentRevision, value, at);
	}

	/// <summary>
	///     Records one answer against an exact revision — the current one, or a
	///     known, non-deleted, superseded one a reporter's browser session spanned
	///     an Administrator's edit across. Validation always runs against that exact
	///     revision's historical type and privacy, and the question's live choices; a submission never has
	///     to equal the latest form.
	/// </summary>
	public ReportAnswer Answer(Question question,
							   QuestionRevision revision,
							   string? value,
							   DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(question);
		ArgumentNullException.ThrowIfNull(revision);

		var answer = ReportAnswer.For(Id, question, revision, value, Language, at);
		_answers.Add(answer);
		Project(question, answer);
		return answer;
	}

	/// <summary>
	///     Records a multi-select answer against the question's current revision, as
	///     one row per chosen value, so each value is a string in its own right and
	///     is translated on its own (ADR-0072). An empty list records one skipped
	///     answer rather than nothing at all.
	/// </summary>
	public IReadOnlyList<ReportAnswer> Answer(
		Question question,
		IReadOnlyList<string> values,
		DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(question);

		return Answer(question, question.CurrentRevision, values, at);
	}

	/// <summary>
	///     Records a multi-select answer against an exact revision. See the
	///     single-value overload above for why the revision is explicit.
	/// </summary>
	public IReadOnlyList<ReportAnswer> Answer(
		Question question,
		QuestionRevision revision,
		IReadOnlyList<string> values,
		DateTimeOffset at)
	{
		ArgumentNullException.ThrowIfNull(question);
		ArgumentNullException.ThrowIfNull(revision);
		ArgumentNullException.ThrowIfNull(values);

		if (values.Count == 0)
		{
			return [Answer(question, revision, value: null, at)];
		}

		// Multi-select is the only type that produces several rows. Everything
		// else — a picker, a date, a line of prose — is one answer.
		if (values.Count > 1
			&& revision.Type != QuestionType.MultiSelect)
		{
			throw new DomainRuleViolationException(
				$"'{question.Key}' takes one answer, not {values.Count}.");
		}

		return [.. values.Select(value => Answer(question, revision, value, at))];
	}

	/// <summary>Adds an uploaded file.</summary>
	public ReportFile AddFile(string blobKey,
							  string contentType,
							  long byteSize,
							  DateTimeOffset uploadedAt)
	{
		var file = new ReportFile(Id, blobKey, contentType, byteSize, uploadedAt);
		_files.Add(file);
		return file;
	}

	/// <summary>
	///     Adds a claimed upload under the id its blobs were written with, keeping the
	///     reporter's sanitized filename (ADR-0097).
	/// </summary>
	public ReportFile AddFile(TinyId fileId,
							  string blobKey,
							  string contentType,
							  long byteSize,
							  string? originalFileName,
							  DateTimeOffset uploadedAt)
	{
		var file = new ReportFile(fileId, Id, blobKey, contentType, byteSize, originalFileName, uploadedAt);
		_files.Add(file);
		return file;
	}

	/// <summary>Attaches the report's bilingual summary, once the Worker has produced one.</summary>
	public void AttachSummary(Summary summary)
	{
		ArgumentNullException.ThrowIfNull(summary);

		if (Summary is not null)
		{
			throw new DomainRuleViolationException("This report already has a summary.");
		}

		Summary = summary;
	}

	/// <summary>
	///     Refuses a submission that has not answered publication consent. The form
	///     enforces this too; this is the enforcement that cannot be skipped by
	///     posting to the API directly.
	/// </summary>
	public void EnsureReadyForSubmission()
	{
		if (!HasAnsweredConsent)
		{
			throw new DomainRuleViolationException(
				"A report cannot be submitted until the publication-consent question is answered yes or no.");
		}
	}

	/// <summary>The worker has claimed this report.</summary>
	public void BeginSummarizing()
	{
		Status = ReportStatus.Summarizing;
	}

	/// <summary>Summaries exist; a human now has to look at them.</summary>
	public void AwaitReview()
	{
		SummaryError = null;
		Status = ReportStatus.PendingReview;
	}

	/// <summary>
	///     Summarization failed. The report still lands in front of a safety officer
	///     with the error attached, so it can never become invisible.
	/// </summary>
	public void FailSummarization(string error)
	{
		SummaryError = error;
		Status = ReportStatus.SummaryFailed;
	}

	/// <summary>
	///     The reporter did not consent to publication, so the Worker never sends
	///     the report to the model: it goes to review with no summary and can never
	///     be published (REQ-DOM-006, REQ-AI-027).
	/// </summary>
	public void ReviewWithoutSummary()
	{
		EnsureLive();
		EnsureIn("go to review without a summary", ReportStatus.Submitted, ReportStatus.Summarizing);

		if (ConsentPublish is true)
		{
			throw new DomainRuleViolationException("A report with publication consent is summarized before review.");
		}

		Status = ReportStatus.PendingReview;
	}

	/// <summary>A safety officer approved the report.</summary>
	public void Approve()
	{
		Status = ReportStatus.Approved;
	}

	/// <summary>A safety officer rejected the report.</summary>
	public void Reject()
	{
		Status = ReportStatus.Rejected;
	}

	/// <summary>
	///     Marks the report published. Refused unless <see cref="IsPublishable" /> —
	///     the consent gate and the human gate are checked here, not by the caller.
	/// </summary>
	public void MarkPublished(DateTimeOffset at)
	{
		if (ConsentPublish is not true)
		{
			throw new DomainRuleViolationException(
				ConsentPublish is null
					? "This report has no answer to the publication-consent question. An unanswered consent is not a consent."
					: "This reporter did not consent to publication. The report is stored, summarized, and counted internally, and never published.");
		}

		if (!IsPublishable)
		{
			throw new DomainRuleViolationException(
				"A report is published only once a safety officer has approved it and its summary pair.");
		}

		Status = ReportStatus.Published;
		PublishedAt = at;
	}

	/// <summary>The longest rejection note a reviewer may write.</summary>
	public const int RejectionNoteMaxLength = 2000;

	/// <summary>The provenance a hand-written summary pair carries as its model and prompt version.</summary>
	public const string ManualProvenance = "manual";

	/// <summary>
	///     A reviewer approves the current pair (REQ-MOD-033). When the reporter
	///     consented, the report is published in the same action; otherwise it is
	///     Approved and never public (ADR-0105). Every publication guard still runs.
	/// </summary>
	/// <returns>True when the approval also published the report.</returns>
	public bool ApprovePair(string approverSubject,
							DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(approverSubject);
		EnsureLive();
		EnsureIn("approve the pair", ReportStatus.PendingReview);

		var summary = Summary ?? throw new DomainRuleViolationException("There is no summary pair to approve.");
		summary.Approve(approverSubject, at);
		Status = ReportStatus.Approved;

		if (ConsentPublish is not true)
		{
			return false;
		}

		MarkPublished(at);
		return true;
	}

	/// <summary>
	///     A reviewer rejects the report, optionally saying why. It stays for internal
	///     learning and can never be published while rejected (REQ-MOD-034, REQ-MOD-058).
	/// </summary>
	public void RejectReview(string? note)
	{
		EnsureLive();
		EnsureIn("reject the report", ReportStatus.PendingReview);

		note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

		if (note is { Length: > RejectionNoteMaxLength })
		{
			throw new DomainRuleViolationException(
				$"A rejection note is at most {RejectionNoteMaxLength} characters.");
		}

		RejectionNote = note;
		Status = ReportStatus.Rejected;
	}

	/// <summary>A reviewer changes their mind: a rejected report returns to review (REQ-MOD-056).</summary>
	public void Reopen()
	{
		EnsureLive();
		EnsureIn("reopen the report", ReportStatus.Rejected);

		RejectionNote = null;
		Status = ReportStatus.PendingReview;
	}

	/// <summary>
	///     A reviewer takes a published report down without editing it. Approval is
	///     cleared and the report returns to review (REQ-MOD-057).
	/// </summary>
	public void Unpublish()
	{
		EnsureLive();
		EnsureIn("unpublish the report", ReportStatus.Published);

		// A published report always has an approved pair; publication requires it.
		Summary!.ClearApproval();
		PublishedAt = null;
		Status = ReportStatus.PendingReview;
	}

	/// <summary>
	///     A reviewer saves both texts of the pair together. Approval is cleared and
	///     the report returns to review, off the public feed if it was on it
	///     (REQ-MOD-032, REQ-DOM-005). A language whose text did not change keeps how
	///     it was produced; a changed one records <paramref name="sourceEn" /> or
	///     <paramref name="sourceFr" /> (ADR-0108).
	/// </summary>
	public void EditSummary(string textEn,
							string textFr,
							DateTimeOffset at,
							SummaryTextSource sourceEn = SummaryTextSource.Human,
							SummaryTextSource sourceFr = SummaryTextSource.Human)
	{
		EnsureLive();
		EnsureIn("edit a summary text", ReportStatus.PendingReview, ReportStatus.Approved, ReportStatus.Published);

		var summary = Summary ?? throw new DomainRuleViolationException("There is no summary pair to edit.");

		if (!string.Equals(textEn, summary.AiSummaryEn, StringComparison.Ordinal))
		{
			summary.RewriteEn(textEn, at, sourceEn);
		}

		if (!string.Equals(textFr, summary.AiSummaryFr, StringComparison.Ordinal))
		{
			summary.RewriteFr(textFr, at, sourceFr);
		}

		// Saving is a review decision even when nothing changed: approval always
		// clears and the report always returns to review.
		summary.ClearApproval();
		PublishedAt = null;
		Status = ReportStatus.PendingReview;
	}

	/// <summary>
	///     Summarization failed, so a reviewer writes the pair by hand. It carries
	///     <see cref="ManualProvenance" /> as its model and prompt version, each
	///     language records whether it was typed or an accepted translation, and it
	///     goes to review like any other pair (REQ-MOD-059, ADR-0108).
	/// </summary>
	public void WriteManualSummary(string textEn,
								   string textFr,
								   DateTimeOffset at,
								   SummaryTextSource sourceEn = SummaryTextSource.Human,
								   SummaryTextSource sourceFr = SummaryTextSource.Human)
	{
		EnsureLive();
		EnsureIn("write a manual pair", ReportStatus.SummaryFailed);

		var summary = Summary.Generate(Id, textEn, textFr, ManualProvenance, ManualProvenance, at);
		summary.RewriteEn(textEn, at, sourceEn);
		summary.RewriteFr(textFr, at, sourceFr);
		AttachSummary(summary);
		AwaitReview();
	}

	private void EnsureLive()
	{
		if (Deleted is not null)
		{
			throw new DomainRuleViolationException("This report was deleted.");
		}
	}

	private void EnsureIn(string action,
						  params ReportStatus[] allowed)
	{
		if (!allowed.Contains(Status))
		{
			throw new ReviewTransitionException(action, Status);
		}
	}

	/// <summary>
	///     Soft-deletes this report and everything it owns — its answers, files, and
	///     summary — with one shared timestamp, in memory. The caller persists it and
	///     the report's outbox rows (not part of this aggregate) in the same
	///     transaction, so the whole cascade commits or fails together
	///     (REQ-DOM-007). Irreversible: there is no restore transition.
	/// </summary>
	public void SoftDelete(DateTimeOffset at)
	{
		if (Deleted is not null)
		{
			return;
		}

		Deleted = at;

		foreach (var answer in _answers)
		{
			answer.Delete(at);
		}

		foreach (var file in _files)
		{
			file.Delete(at);
		}

		Summary?.Delete(at);
	}

	private void Project(Question question,
						 ReportAnswer answer)
	{
		if (question.Role == QuestionRole.ConsentPublish)
		{
			ConsentPublish = ReadConsent(answer, "Publication consent");
		}
		else if (question.Role == QuestionRole.ConsentMedia)
		{
			ConsentMedia = ReadConsent(answer, "Media consent");
		}
	}

	/// <summary>
	///     Reads a yes or a no, and refuses anything else. The consent question has
	///     no default answer, so an unreadable one is an error rather than a
	///     silently negative consent.
	/// </summary>
	private static bool ReadConsent(ReportAnswer answer,
									string consent)
	{
		// "yes" and "no" are the invariant stored forms of every boolean answer
		// (ADR-0072), so this reads the same two tokens whichever language the
		// reporter used.
		if (string.Equals(answer.Value, "yes", StringComparison.Ordinal))
		{
			return true;
		}

		if (string.Equals(answer.Value, "no", StringComparison.Ordinal))
		{
			return false;
		}

		throw new DomainRuleViolationException(
			$"{consent} must be answered yes or no. There is no default and no third state.");
	}
}
