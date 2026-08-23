using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>A submitted report whose ordinary data remains question answers.</summary>
public sealed class Report
{
    private readonly List<ReportAnswer> _answers = [];
    private readonly List<Summary> _summaries = [];

    private Report()
    {
    }

    /// <summary>Creates a report in the language used by the reporter.</summary>
    public Report(Locale language, DateTimeOffset submittedAt)
    {
        Id = TinyId.New();
        Language = language;
        SubmittedAt = submittedAt;
        Status = ReportStatus.Submitted;
    }

    /// <summary>Report id.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Language used for answers and the summary.</summary>
    public Locale Language { get; private init; }

    /// <summary>Lifecycle state.</summary>
    public ReportStatus Status { get; private set; }

    /// <summary>Submission instant.</summary>
    public DateTimeOffset SubmittedAt { get; private init; }

    /// <summary>Explicit publication consent; null means unanswered.</summary>
    public bool? ConsentPublish { get; private set; }

    /// <summary>Content-free Worker error.</summary>
    public string? SummaryError { get; private set; }

    /// <summary>One row per answer-producing question shown, including skips.</summary>
    public IReadOnlyList<ReportAnswer> Answers => _answers;

    /// <summary>The single candidate summary.</summary>
    public IReadOnlyList<Summary> Summaries => _summaries;

    /// <summary>True only after positive consent and human approval.</summary>
    public bool IsPublishable =>
        ConsentPublish is true
        && Status is ReportStatus.Approved or ReportStatus.Published
        && _summaries.Count == 1
        && _summaries[0].IsApproved;

    /// <summary>Records a nullable text answer to the exact question revision.</summary>
    public ReportAnswer Answer(Question question, string? value, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(question);
        EnsureNotAnswered(question);

        var answer = ReportAnswer.ForText(Id, question, value, at);
        _answers.Add(answer);
        ProjectConsent(question, answer);
        return answer;
    }

    /// <summary>Records option codes to the exact question revision.</summary>
    public ReportAnswer Answer(Question question, IReadOnlyList<string> optionCodes, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(optionCodes);
        EnsureNotAnswered(question);

        var answer = ReportAnswer.ForOptions(Id, question, optionCodes, at);
        _answers.Add(answer);
        ProjectConsent(question, answer);
        return answer;
    }

    /// <summary>Requires an explicit yes/no consent answer before persistence completes.</summary>
    public void EnsureReadyForSubmission()
    {
        if (ConsentPublish is null)
        {
            throw new DomainRuleViolationException(
                "A report cannot be submitted until publication consent is answered yes or no.");
        }
    }

    /// <summary>Adds the one AI or manually authored summary.</summary>
    public void AddSummary(Summary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.ReportId != Id)
        {
            throw new DomainRuleViolationException("A summary must belong to this report.");
        }

        if (_summaries.Count > 0)
        {
            throw new DomainRuleViolationException("This report already has a summary.");
        }

        _summaries.Add(summary);
    }

    /// <summary>Marks the report as claimed by the Worker.</summary>
    public void BeginSummarizing() => Status = ReportStatus.Summarizing;

    /// <summary>Moves a successful summary to human review.</summary>
    public void AwaitReview()
    {
        SummaryError = null;
        Status = ReportStatus.PendingReview;
    }

    /// <summary>Leaves a failed report visible for a manual summary.</summary>
    public void FailSummarization(string error)
    {
        SummaryError = string.IsNullOrWhiteSpace(error) ? "Summarization failed." : error;
        Status = ReportStatus.SummaryFailed;
    }

    /// <summary>Records human approval.</summary>
    public void Approve() => Status = ReportStatus.Approved;

    /// <summary>Records human rejection.</summary>
    public void Reject() => Status = ReportStatus.Rejected;

    /// <summary>Publishes only a consented, human-approved summary.</summary>
    public void MarkPublished()
    {
        if (!IsPublishable)
        {
            throw new DomainRuleViolationException(
                "Publication requires positive consent and one human-approved summary.");
        }

        Status = ReportStatus.Published;
    }

    private void EnsureNotAnswered(Question question)
    {
        if (_answers.Exists(answer => answer.QuestionId == question.Id))
        {
            throw new DomainRuleViolationException($"'{question.Key}' was already answered.");
        }
    }

    private void ProjectConsent(Question question, ReportAnswer answer)
    {
        if (question.Key != QuestionKey.ConsentPublish)
        {
            return;
        }

        ConsentPublish = answer.SingleOptionCode switch
        {
            "yes" => true,
            "no" => false,
            _ => throw new DomainRuleViolationException("Publication consent must be answered yes or no."),
        };
    }
}
