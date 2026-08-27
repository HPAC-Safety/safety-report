
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// An occurrence report. Every ordinary answer is data — one row per question
/// asked, in <see cref="Answers"/> — and publication consent is the only one
/// that additionally projects onto a typed property here, because it is read by
/// logic rather than only displayed. See <c>docs/data-and-persistence.md</c>.
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

    public Report(Locale language, DateTimeOffset submittedAt)
    {
        Id = TinyId.New();
        Language = language;
        SubmittedAt = submittedAt;
        Status = ReportStatus.Submitted;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>
    /// The locale the report was written in. The Worker summarizes in this
    /// language and produces the other in the same call; the raw narrative is
    /// never translated.
    /// </summary>
    public Locale Language { get; private init; }

    /// <summary>Where the report is in its lifecycle.</summary>
    public ReportStatus Status { get; private set; }

    /// <summary>When it was received.</summary>
    public DateTimeOffset SubmittedAt { get; private init; }

    /// <summary>When it was published, if it was.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// Whether the reporter agreed to publication of a de-identified version.
    /// <b>Null means unanswered</b>, which is a different thing from "no": the
    /// consent question is required and has no default, so a reporter must
    /// choose one before submitting. Silence is not consent, and neither is a
    /// pre-selected radio button.
    /// </summary>
    public bool? ConsentPublish { get; private set; }

    /// <summary>True once the reporter has actually chosen yes or no.</summary>
    public bool HasAnsweredConsent => ConsentPublish is not null;

    /// <summary>Why summarization failed, when it did. Attached so the report
    /// still reaches a human rather than disappearing.</summary>
    public string? SummaryError { get; private set; }

    /// <summary>When this report was soft-deleted, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>Every answer given, against the question revision it was given under.</summary>
    public IReadOnlyList<ReportAnswer> Answers => _answers;

    /// <summary>Uploaded attachments.</summary>
    public IReadOnlyList<ReportFile> Files => _files;

    /// <summary>The bilingual summary, once the Worker has produced one.</summary>
    public Summary? Summary { get; private set; }

    /// <summary>
    /// True only when a reporter consented, a safety officer approved the report,
    /// and the summary pair was approved. Every clause is load bearing: nothing
    /// reaches the public without all three.
    /// </summary>
    public bool IsPublishable =>
        ConsentPublish is true
        && Status is ReportStatus.Approved or ReportStatus.Published
        && Summary is { IsApproved: true };

    /// <summary>Records a free-text answer, projecting it if the question carries a role.</summary>
    public ReportAnswer Answer(Question question, string? value, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(question);

        var answer = ReportAnswer.ForText(Id, question, value, at);
        _answers.Add(answer);
        Project(question, answer);
        return answer;
    }

    /// <summary>Records a select answer, projecting it if the question carries a role.</summary>
    public ReportAnswer Answer(Question question, IReadOnlyList<string> optionCodes, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(optionCodes);

        var answer = ReportAnswer.ForOptions(Id, question, optionCodes, at);
        _answers.Add(answer);
        Project(question, answer);
        return answer;
    }

    /// <summary>Adds an uploaded file.</summary>
    public ReportFile AddFile(string blobKey, string contentType, long byteSize, DateTimeOffset uploadedAt)
    {
        var file = new ReportFile(Id, blobKey, contentType, byteSize, uploadedAt);
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
    /// Refuses a submission that has not answered publication consent. The form
    /// enforces this too; this is the enforcement that cannot be skipped by
    /// posting to the API directly.
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
    public void BeginSummarizing() => Status = ReportStatus.Summarizing;

    /// <summary>Summaries exist; a human now has to look at them.</summary>
    public void AwaitReview()
    {
        SummaryError = null;
        Status = ReportStatus.PendingReview;
    }

    /// <summary>
    /// Summarization failed. The report still lands in front of a safety officer
    /// with the error attached, so it can never become invisible.
    /// </summary>
    public void FailSummarization(string error)
    {
        SummaryError = error;
        Status = ReportStatus.SummaryFailed;
    }

    /// <summary>A safety officer approved the report.</summary>
    public void Approve() => Status = ReportStatus.Approved;

    /// <summary>A safety officer rejected the report.</summary>
    public void Reject() => Status = ReportStatus.Rejected;

    /// <summary>
    /// Marks the report published. Refused unless <see cref="IsPublishable"/> —
    /// the consent gate and the human gate are checked here, not by the caller.
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

    private void Project(Question question, ReportAnswer answer)
    {
        if (question.Role == QuestionRole.ConsentPublish)
        {
            ConsentPublish = ReadConsent(answer);
        }
    }

    /// <summary>
    /// Reads a yes or a no, and refuses anything else. The consent question has
    /// no default answer, so an unreadable one is an error rather than a
    /// silently negative consent.
    /// </summary>
    private static bool ReadConsent(ReportAnswer answer)
    {
        var given = answer.SingleOptionCode ?? answer.Value;

        if (string.Equals(given, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(given, bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(given, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(given, bool.FalseString, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new DomainRuleViolationException(
            "Publication consent must be answered yes or no. There is no default and no third state.");
    }
}
