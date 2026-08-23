
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>
/// An occurrence report. The answers are data — one row per question asked — and
/// a handful of them additionally project onto typed properties here, because
/// consent, injury severity, the occurrence date, and the province are read by
/// logic rather than only displayed.
/// </summary>
/// <remarks>
/// Which answer projects where comes from <see cref="QuestionRole"/>, which an
/// administrator can move between questions. Every projection except consent is
/// optional: with no injury question on the form, <see cref="PilotInjury"/> is
/// <see cref="InjurySeverity.NotAnswered"/> and the report takes the ordinary
/// review path.
/// </remarks>
public class Report
{
    private readonly List<ReportAnswer> _answers = [];
    private readonly List<ReportAircraft> _aircraft = [];
    private readonly List<ReportFile> _files = [];
    private readonly List<Summary> _summaries = [];

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
    /// The locale the report was written in. The summary is generated in this
    /// language and translated into the other; the raw narrative is never
    /// translated.
    /// </summary>
    public Locale Language { get; private init; }

    /// <summary>Where the report is in its lifecycle.</summary>
    public ReportStatus Status { get; private set; }

    /// <summary>When it was received.</summary>
    public DateTimeOffset SubmittedAt { get; private init; }

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

    /// <summary>The date of the occurrence, if the form asked for one.</summary>
    public DateOnly? OccurredOn { get; private set; }

    /// <summary>
    /// The clock time at the site, as the reporter gave it. Local wall clock,
    /// not an instant: "morning" means what the clock on the wall said, and this
    /// system collects a province rather than coordinates, so any offset it
    /// stored would be inferred. Restricted, and encrypted at rest.
    /// <para>
    /// Null when the reporter did not give one — #68 makes the time optional so
    /// that somebody who does not remember still files. That reads as
    /// <see cref="TimeOfDay.Unknown"/>, never as midnight. See ADR-0019.
    /// </para>
    /// </summary>
    public TimeOnly? OccurredAtLocal { get; private set; }

    /// <summary>The province, if the form asked for one.</summary>
    public Province Province { get; private set; } = Province.NotAnswered;

    /// <summary>Coarse time of day, if the form asked for one.</summary>
    public TimeOfDay TimeOfDay { get; private set; } = TimeOfDay.NotAnswered;

    /// <summary>Injury to the pilot, if the form asked.</summary>
    public InjurySeverity PilotInjury { get; private set; } = InjurySeverity.NotAnswered;

    /// <summary>Injury to the passenger, if the form asked.</summary>
    public InjurySeverity PassengerInjury { get; private set; } = InjurySeverity.NotAnswered;

    /// <summary>Why summarization failed, when it did. Attached so the report
    /// still reaches a human rather than disappearing.</summary>
    public string? SummaryError { get; private set; }

    /// <summary>Every answer given, against the question version it was given under.</summary>
    public IReadOnlyList<ReportAnswer> Answers => _answers;

    /// <summary>The aircraft involved.</summary>
    public IReadOnlyList<ReportAircraft> Aircraft => _aircraft;

    /// <summary>Uploaded media.</summary>
    public IReadOnlyList<ReportFile> Files => _files;

    /// <summary>The summaries, one per official locale.</summary>
    public IReadOnlyList<Summary> Summaries => _summaries;

    /// <summary>
    /// True only when a reporter consented, a safety officer approved the report,
    /// and both languages of the summary were approved. Every clause is load
    /// bearing: nothing reaches the public without all four.
    /// </summary>
    public bool IsPublishable =>
        ConsentPublish is true
        && Status is ReportStatus.Approved or ReportStatus.Published
        && Locale.All.All(locale => _summaries.Exists(s => s.Locale == locale && s.IsApproved));

    /// <summary>True when a serious injury or fatality was reported for anyone aboard.</summary>
    public bool InvolvesSeriousInjury =>
        PilotInjury is InjurySeverity.Serious or InjurySeverity.Fatality
        || PassengerInjury is InjurySeverity.Serious or InjurySeverity.Fatality;

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

    /// <summary>Adds an aircraft.</summary>
    public ReportAircraft AddAircraft(Discipline discipline, string? manufacturer, string? model, string? certificationAnswer)
    {
        var aircraft = new ReportAircraft(Id, discipline, manufacturer, model, certificationAnswer);
        _aircraft.Add(aircraft);
        return aircraft;
    }

    /// <summary>Adds an uploaded file.</summary>
    public ReportFile AddFile(string blobKey, string contentType, long byteSize, DateTimeOffset uploadedAt)
    {
        var file = new ReportFile(Id, blobKey, contentType, byteSize, uploadedAt);
        _files.Add(file);
        return file;
    }

    /// <summary>Attaches a summary in one language.</summary>
    public void AddSummary(Summary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (_summaries.Exists(s => s.Locale == summary.Locale))
        {
            throw new DomainRuleViolationException($"This report already has a {summary.Locale} summary.");
        }

        _summaries.Add(summary);
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
    public void MarkPublished()
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
                "A report is published only once a safety officer has approved it and both languages of its summary.");
        }

        Status = ReportStatus.Published;
    }

    private void Project(Question question, ReportAnswer answer)
    {
        switch (question.Role)
        {
            case QuestionRole.ConsentPublish:
                ConsentPublish = ReadConsent(answer);
                break;

            case QuestionRole.OccurrenceDate:
                if (DateOnly.TryParse(answer.Value, System.Globalization.CultureInfo.InvariantCulture, out var date))
                {
                    OccurredOn = date;
                }

                break;

            case QuestionRole.OccurrenceTime:
                // The reporter gives a real time; the coarse bucket is derived
                // from it. An unreadable or absent answer is "do not know" —
                // a defined state, never a silent midnight.
                OccurredAtLocal =
                    TimeOnly.TryParse(answer.Value, System.Globalization.CultureInfo.InvariantCulture, out var time)
                        ? time
                        : null;
                TimeOfDay = Reporting.TimeOfDay.FromLocalTime(OccurredAtLocal);
                break;

            case QuestionRole.Province:
                if (EnumCode.TryParse<Province>(answer.SingleOptionCode, out var province))
                {
                    Province = province;
                }

                break;

            case QuestionRole.PilotInjury:
                if (EnumCode.TryParse<InjurySeverity>(answer.SingleOptionCode, out var pilotInjury))
                {
                    PilotInjury = pilotInjury;
                }

                break;

            case QuestionRole.PassengerInjury:
                if (EnumCode.TryParse<InjurySeverity>(answer.SingleOptionCode, out var passengerInjury))
                {
                    PassengerInjury = passengerInjury;
                }

                break;

            case QuestionRole.None:
            case QuestionRole.AircraftType:
            case QuestionRole.AircraftCertification:
            case QuestionRole.Narrative:
            default:
                break;
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
