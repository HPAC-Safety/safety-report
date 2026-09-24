using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The report lifecycle as the domain enforces it: REQ-DOM-001's transitions,
///     REQ-DOM-014's refusals, and REQ-DOM-005's unpublish-on-edit. These are rules
///     of the <see cref="Report" /> aggregate, so they run against it directly.
/// </summary>
[Binding]
[Scope(Feature = "Domain and lifecycle")]
public sealed class ReviewLifecycleSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string Officer = "synthetic-officer";
	private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

	private Report _report = null!;
	private ReportStatus _from;
	private Exception? _refusal;

	[Given(@"^a report is in state (\w+)$")]
	public void GivenAReportIsInState(string state)
	{
		_from = Enum.Parse<ReportStatus>(state);
		_report = In(_from, consent: "yes");
	}

	[When(@"^(.+) occurs$")]
	public void WhenAnEventOccurs(string lifecycleEvent)
	{
		switch (lifecycleEvent)
		{
			case "Worker claims the summary job":
				_report.BeginSummarizing();
				break;
			case "a valid bilingual pair is saved":
				_report.AttachSummary(Summary.Generate(_report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
				_report.AwaitReview();
				break;
			case "bounded retries are exhausted":
				_report.FailSummarization("The provider was unavailable.");
				break;
			case "an officer writes both texts":
				_report.WriteManualSummary("The pilot landed.", "Le pilote s'est posé.", Now);
				break;
			case "either summary text is edited":
				_report.EditSummary("The pilot landed firmly.", "Le pilote s'est posé fermement.", Now);
				break;
			case "an officer approves the pair and consent is yes":
				_report.ApprovePair(Officer, Now).ShouldBeTrue();
				break;
			case "an officer approves the pair and consent is no":
				_report = In(ReportStatus.PendingReview, consent: "no");
				_report.ApprovePair(Officer, Now).ShouldBeFalse();
				break;
			case "an officer rejects the report":
				_report.RejectReview(null);
				break;
			case "an officer unpublishes the report":
				_report.Unpublish();
				break;
			case "an officer reopens the report":
				_report.Reopen();
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(lifecycleEvent), lifecycleEvent, "No such lifecycle event.");
		}
	}

	[Then(@"^the report moves to state (\w+)$")]
	public void ThenTheReportMovesTo(string state)
	{
		_report.Status.ShouldBe(Enum.Parse<ReportStatus>(state));
	}

	[When(@"^an officer tries to (.+)$")]
	public void WhenAnOfficerTries(string action)
	{
		Action attempt = action switch
		{
			"approve the pair" => () => _report.ApprovePair(Officer, Now),
			"edit a summary text" => () => _report.EditSummary("en", "fr", Now),
			"reject the report" => () => _report.RejectReview(null),
			"reopen the report" => _report.Reopen,
			"unpublish the report" => _report.Unpublish,
			"write a manual pair" => () => _report.WriteManualSummary("en", "fr", Now),
			_ => throw new ArgumentOutOfRangeException(nameof(action), action, "No such review action."),
		};

		_refusal = Record.Exception(attempt);
	}

	[Then(@"the action is refused")]
	public void ThenTheActionIsRefused()
	{
		_refusal.ShouldBeOfType<ReviewTransitionException>();
	}

	[Then(@"^the report stays in state (\w+)$")]
	public void ThenTheReportStaysIn(string state)
	{
		_report.Status.ShouldBe(Enum.Parse<ReportStatus>(state));
		_report.Status.ShouldBe(_from);
	}

	[Given(@"a report is Published")]
	public void GivenAReportIsPublished()
	{
		_report = In(ReportStatus.Published, consent: "yes");
		_report.IsPublishable.ShouldBeTrue();
	}

	[When(@"either the English or French summary text is edited")]
	public void WhenEitherTextIsEdited()
	{
		_report.EditSummary("The pilot landed firmly.", _report.Summary!.AiSummaryFr, Now);
	}

	[Then(@"the pair's approver subject and approval timestamp are cleared")]
	public void ThenApprovalIsCleared()
	{
		_report.Summary!.ApprovedBySubject.ShouldBeNull();
		_report.Summary.ApprovedAt.ShouldBeNull();
	}

	[Then(@"the report immediately stops satisfying the publication invariant")]
	public void ThenTheReportIsNoLongerPublishable()
	{
		_report.IsPublishable.ShouldBeFalse();
		_report.PublishedAt.ShouldBeNull();
	}

	/// <summary>Builds a report in <paramref name="status" /> through the lifecycle itself, never by setting it.</summary>
	internal static Report In(ReportStatus status,
							  string consent)
	{
		var report = new Report(Locale.EnCa, Now);
		report.Answer(
			Question.CreateConsentPublish("May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", Now),
			[consent],
			Now);

		if (status == ReportStatus.Submitted)
		{
			return report;
		}

		report.BeginSummarizing();

		switch (status)
		{
			case ReportStatus.Summarizing:
				return report;
			case ReportStatus.SummaryFailed:
				report.FailSummarization("The provider was unavailable.");
				return report;
		}

		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", Now));
		report.AwaitReview();

		switch (status)
		{
			case ReportStatus.PendingReview:
				break;
			case ReportStatus.Approved:
				report = In(ReportStatus.PendingReview, consent: "no");
				report.ApprovePair(Officer, Now);
				break;
			case ReportStatus.Published:
				report.ApprovePair(Officer, Now);
				break;
			case ReportStatus.Rejected:
				report.RejectReview(null);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(status), status, "No such status.");
		}

		report.Status.ShouldBe(status);
		return report;
	}
}
