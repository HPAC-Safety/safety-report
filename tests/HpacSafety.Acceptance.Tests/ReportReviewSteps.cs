using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The admin report list and read-only detail view, through the booted API
///     (REQ-MOD-030, REQ-MOD-031, REQ-MOD-049..051). The booted database is shared
///     by every scenario, so assertions name the reports seeded here.
/// </summary>
[Binding]
public sealed class ReportReviewSteps
{
	private const string PilotName = "Rowan Synthetic";
	private const string SummaryEn = "The pilot landed in a field.";

	private static readonly SemaphoreSlim ConsentGate = new(1, 1);

	private readonly Dictionary<string, string> _seeded = new(StringComparer.Ordinal);
	private List<JsonElement> _listed = [];
	private JsonElement _detail;
	private string _detailBody = string.Empty;
	private string _listBody = string.Empty;
	private string _openedId = string.Empty;

	// ── Given ───────────────────────────────────────────────────────────────

	[Given(@"reports exist in every workflow state, one without publication consent, and one soft-deleted")]
	[Given(@"reports exist in every workflow state")]
	public async Task GivenReportsInEveryState()
	{
		if (_seeded.Count == 0)
		{
			await Seed();
		}
	}

	[Given(@"one report has waited in Submitted and one in Summarizing for more than 24 hours")]
	[Given(@"one report has waited in Summarizing for less than 24 hours")]
	public void GivenTheStuckAndFreshReports()
	{
		// Seeded with the rest: stuckSubmitted, stuckSummarizing, freshSummarizing.
		_seeded.ShouldContainKey("stuckSubmitted");
		_seeded.ShouldContainKey("freshSummarizing");
	}

	[Given(@"a reviewer opens a report's detail view")]
	public async Task GivenAReviewerOpensADetailView()
	{
		await Seed();
		_openedId = _seeded["pending"];
	}

	// ── When ────────────────────────────────────────────────────────────────

	[When(@"a reviewer lists reports")]
	public async Task WhenAReviewerListsReports()
	{
		await ListWith(null);
	}

	[When(@"a reviewer lists reports needing action")]
	public async Task WhenAReviewerListsReportsNeedingAction()
	{
		await ListWith("needs-action");
	}

	[When(@"a reviewer lists reports with the (.+) filter")]
	public async Task WhenAReviewerListsWithFilter(string filter)
	{
		await ListWith(filter);
	}

	[When(@"the detail query runs")]
	public async Task WhenTheDetailQueryRuns()
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{_openedId}", UriKind.Relative));
		response.EnsureSuccessStatusCode();
		_detailBody = await response.Content.ReadAsStringAsync();
		_detail = JsonDocument.Parse(_detailBody).RootElement;
	}

	// ── Then: the list ──────────────────────────────────────────────────────

	[Then(@"every live report appears, newest first, with its workflow status and whether publication consent was refused")]
	public void ThenEveryLiveReportAppearsNewestFirst()
	{
		string[] expected =
		[
			"pending", "pendingPrivate", "failed", "approved", "published", "rejected", "freshSummarizing",
			"stuckSubmitted", "stuckSummarizing",
		];

		Mine().ShouldBe([.. expected.Select(name => _seeded[name])]);
		Row("pendingPrivate").GetProperty("consent").GetString().ShouldBe("no");
		Row("pending").GetProperty("status").GetString().ShouldBe("pending_review");
		Row("published").GetProperty("status").GetString().ShouldBe("published");
		Row("rejected").GetProperty("status").GetString().ShouldBe("rejected");
	}

	[Then(@"the soft-deleted report does not appear")]
	public void ThenTheDeletedReportDoesNotAppear()
	{
		Mine().ShouldNotContain(_seeded["deleted"]);
	}

	[Then(@"no answer text or summary text appears in the list")]
	public void ThenNoContentAppearsInTheList()
	{
		_listBody.ShouldNotContain(PilotName);
		_listBody.ShouldNotContain(SummaryEn);
	}

	[Then(@"the list holds the pending-review, summary-failed, and two stuck reports")]
	public void ThenTheNeedsActionListHoldsTheRightReports()
	{
		Mine().ShouldBe(
			[.. new[] { "pending", "pendingPrivate", "failed", "stuckSubmitted", "stuckSummarizing" }.Select(name => _seeded[name])],
			ignoreOrder: true);
	}

	[Then(@"each stuck report is marked stuck")]
	public void ThenEachStuckReportIsMarked()
	{
		Row("stuckSubmitted").GetProperty("isStuck").GetBoolean().ShouldBeTrue();
		Row("stuckSummarizing").GetProperty("isStuck").GetBoolean().ShouldBeTrue();
	}

	[Then(@"the report summarizing for less than 24 hours is not listed")]
	public void ThenTheFreshReportIsNotListed()
	{
		Mine().ShouldNotContain(_seeded["freshSummarizing"]);
	}

	[Then(@"the list holds only (.+)")]
	public void ThenTheListHoldsOnly(string reports)
	{
		var expected = reports switch
		{
			"published reports" => "published",
			"live reports whose reporter refused consent" => "pendingPrivate",
			"rejected reports" => "rejected",
			"reports whose summarization failed" => "failed",
			_ => throw new ArgumentOutOfRangeException(nameof(reports), reports, "No seeded report matches."),
		};

		Mine().ShouldBe([_seeded[expected]]);
	}

	// ── Then: the detail view ───────────────────────────────────────────────

	[Then(@"^it supplies the reporter language, exact bilingual question labels and answers with privacy indicated, processing state, both summary texts with their shared provenance/approval, and each attachment's kind and whether it can be opened$")]
	public void ThenTheDetailSuppliesWhatTheReviewerNeeds()
	{
		_detail.GetProperty("language").GetString().ShouldBe("en-CA");
		_detail.GetProperty("status").GetString().ShouldBe("pending_review");

		var pilot = _detail.GetProperty("answers").EnumerateArray()
			.Single(answer => answer.GetProperty("labelEn").GetString() == "Pilot name");
		pilot.GetProperty("labelFr").GetString().ShouldBe("Nom du pilote");
		pilot.GetProperty("isPrivate").GetBoolean().ShouldBeTrue();
		pilot.GetProperty("values")[0].GetProperty("value").GetString().ShouldBe(PilotName);

		var summary = _detail.GetProperty("summary");
		summary.GetProperty("aiSummaryEn").GetString().ShouldBe(SummaryEn);
		summary.GetProperty("aiSummaryFr").GetString().ShouldNotBeNullOrWhiteSpace();
		summary.GetProperty("model").GetString().ShouldBe("gemini-3.7-flash");
		summary.GetProperty("promptVersion").GetString().ShouldBe("summarize-anonymize.v3");
		summary.TryGetProperty("approvedAt", out _).ShouldBeTrue();

		var attachment = _detail.GetProperty("attachments")[0];
		attachment.GetProperty("kind").GetString().ShouldBe("document");
		attachment.GetProperty("state").GetString().ShouldBe("ready");
	}

	[Then(@"it supplies no storage key and no link; an attachment is opened only through its own audited view or download request")]
	public void ThenTheDetailSuppliesNoKeyOrLink()
	{
		_detailBody.ShouldNotContain("/original/");
		_detailBody.ShouldNotContain("http", Case.Insensitive);
	}

	[Then(@"an audit entry records the reviewer's token subject, ViewedRawReport, the report, and the time")]
	public async Task ThenAViewedRawReportRowIsWritten()
	{
		var entry = await ViewedRawReportEntry();
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		entry.TargetType.ShouldBe("Report");
		entry.OccurredAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
	}

	[Then(@"the audit entry records no report content")]
	public async Task ThenTheAuditEntryRecordsNoContent()
	{
		var entry = await ViewedRawReportEntry();
		(entry.Detail ?? string.Empty).ShouldNotContain(PilotName);
		(entry.Detail ?? string.Empty).ShouldNotContain(SummaryEn);
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private async Task<AuditLogEntry> ViewedRawReportEntry()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(_openedId);

		return await database.AuditLog
			.Where(entry => entry.Action == AuditAction.ViewedRawReport && entry.TargetId == reportId)
			.SingleAsync();
	}

	private async Task ListWith(string? filter)
	{
		using var client = await BootedApi.SignedInAs(MemberRole.SafetyOfficer);
		var path = filter is null ? "/api/admin/reports" : $"/api/admin/reports?filter={filter}";
		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));
		response.EnsureSuccessStatusCode();
		_listBody = await response.Content.ReadAsStringAsync();
		_listed = [.. JsonDocument.Parse(_listBody).RootElement.EnumerateArray()];
	}

	private List<string> Mine()
	{
		var mine = _seeded.Values.ToHashSet(StringComparer.Ordinal);
		return [.. _listed.Select(item => item.GetProperty("id").GetString()!).Where(mine.Contains)];
	}

	private JsonElement Row(string name)
	{
		return _listed.Single(item => item.GetProperty("id").GetString() == _seeded[name]);
	}

	/// <summary>
	///     The one consent question, created by whichever scenario needs it first.
	///     Scenarios run in parallel against one database, and its key is unique, so
	///     creation is serialized here and a lost race simply reads the winner's row.
	/// </summary>
	private static async Task<Question> ConsentQuestion(HpacSafetyDbContext database,
														DateTimeOffset at)
	{
		await ConsentGate.WaitAsync();
		try
		{
			var existing = await database.Questions
				.Include(question => question.Revisions)
				.SingleOrDefaultAsync(question => question.Key == QuestionKey.ConsentPublish);

			if (existing is not null)
			{
				return existing;
			}

			var consent = Question.CreateConsentPublish(
				"May we publish a de-identified version?", "Pouvons-nous publier une version anonymisée ?", at);
			database.Questions.Add(consent);

			try
			{
				await database.SaveChangesAsync();
				return consent;
			}
			catch (DbUpdateException)
			{
				// Another step file created it first.
				database.Entry(consent).State = EntityState.Detached;
				return await database.Questions
					.Include(question => question.Revisions)
					.SingleAsync(question => question.Key == QuestionKey.ConsentPublish);
			}
		}
		finally
		{
			ConsentGate.Release();
		}
	}

	/// <summary>
	///     One report per workflow state, submitted a minute apart in the future so
	///     their order is known and no other scenario's reports interleave, plus two
	///     reports stuck for more than a day.
	/// </summary>
	private async Task Seed()
	{
		var factory = await BootedApi.Factory();
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var now = DateTimeOffset.UtcNow;
		var suffix = Guid.NewGuid().ToString("n")[..8];
		var consent = await ConsentQuestion(database, now);

		var pilot = Question.Create($"pilot_{suffix}", QuestionType.ShortText, "Pilot name", "Nom du pilote", now, isPrivate: true);
		database.Questions.Add(pilot);

		var future = now.AddYears(6);
		var offset = 0;

		Report Add(string name,
				   string consentAnswer,
				   DateTimeOffset? submittedAt = null)
		{
			var report = new Report(Locale.EnCa, submittedAt ?? future.AddMinutes(-offset++));
			report.Answer(consent, consentAnswer, now);
			report.Answer(pilot, PilotName, now);
			database.Reports.Add(report);
			_seeded[name] = report.Id.Value;
			return report;
		}

		void Summarize(Report report)
		{
			report.BeginSummarizing();
			report.AttachSummary(Summary.Generate(report.Id, SummaryEn, "Le pilote s'est posé dans un champ.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
			report.AwaitReview();
		}

		var pending = Add("pending", "yes");
		Summarize(pending);
		pending.AddFile(TinyId.New(), $"{pending.Id}/original/doc", "application/pdf", 10, "synthetic.pdf", now);

		Summarize(Add("pendingPrivate", "no"));

		var failed = Add("failed", "yes");
		failed.BeginSummarizing();
		failed.FailSummarization("The AI chat provider was unavailable for this summarization attempt.");

		var approved = Add("approved", "yes");
		Summarize(approved);
		approved.Summary!.Approve("synthetic-approver", now);
		approved.Approve();

		var published = Add("published", "yes");
		Summarize(published);
		published.Summary!.Approve("synthetic-approver", now);
		published.Approve();
		published.MarkPublished(now);

		var rejected = Add("rejected", "yes");
		Summarize(rejected);
		rejected.Reject();

		Add("freshSummarizing", "yes").BeginSummarizing();
		Add("deleted", "yes").SoftDelete(now);
		Add("stuckSubmitted", "yes", now.AddHours(-25));
		Add("stuckSummarizing", "yes", now.AddHours(-26)).BeginSummarizing();

		await database.SaveChangesAsync();
	}
}
