using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The admin report list and read-only detail view, against a real PostgreSQL
///     container (REQ-MOD-030, REQ-MOD-031, REQ-MOD-049..051). The database is shared
///     across the collection, so every assertion names the reports this test seeded
///     rather than counting rows. Every report here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ReportReviewEndpointTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenReportsAreListed_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/reports", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Theory]
	[InlineData("/api/admin/reports")]
	[InlineData("/api/admin/reports/abcdefghijk")]
	[InlineData("/api/admin/counts")]
	public async Task GivenUserRole_WhenReportListOrDetailIsRead_ThenApiForbids(string path)
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenReportsInEveryState_WhenListed_ThenEveryLiveReportAppearsNewestFirstWithStatusAndConsent()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var listed = await List(client, null);

		// Then
		var mine = listed.Where(item => seeded.Ids.Contains(item.GetProperty("id").GetString()!)).ToList();
		mine.Select(item => item.GetProperty("id").GetString())
			.ShouldBe(seeded.LiveNewestFirst);
		Item(listed, seeded["private"]).GetProperty("consent").GetBoolean().ShouldBeFalse();
		Item(listed, seeded["private"]).GetProperty("status").GetString().ShouldBe("unpublished");
		Item(listed, seeded["published"]).GetProperty("consent").GetBoolean().ShouldBeTrue();
		Item(listed, seeded["published"]).GetProperty("status").GetString().ShouldBe("published");
		Item(listed, seeded["pending"]).GetProperty("status").GetString().ShouldBe("pending");
		Item(listed, seeded["pending"]).GetProperty("language").GetString().ShouldBe("en-CA");
		listed.ShouldNotContain(item => item.GetProperty("id").GetString() == seeded["deleted"]);
	}

	[Fact]
	public async Task GivenReports_WhenListed_ThenNoAnswerOrSummaryTextIsInTheList()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/reports", UriKind.Relative));
		var body = await response.Content.ReadAsStringAsync();

		// Then
		body.ShouldContain(seeded["pending"]);
		body.ShouldNotContain(Seeding.NarrativeText);
		body.ShouldNotContain(Seeding.SummaryEn);
		body.ShouldNotContain(Seeding.PilotName);
	}

	[Fact]
	public async Task GivenStuckAndFreshReports_WhenNeedsActionIsListed_ThenPendingFailedAndStuckOnly()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var listed = await List(client, "needs-action");

		// Then
		var ids = listed.Select(item => item.GetProperty("id").GetString()).ToHashSet();
		ids.ShouldContain(seeded["pending"]);
		ids.ShouldContain(seeded["failed"]);
		ids.ShouldContain(seeded["stuckSubmitted"]);
		ids.ShouldContain(seeded["stuckSummarizing"]);
		ids.ShouldNotContain(seeded["freshSummarizing"]);
		ids.ShouldNotContain(seeded["private"]);
		ids.ShouldNotContain(seeded["unpublished"]);
		ids.ShouldNotContain(seeded["published"]);
		Item(listed, seeded["stuckSubmitted"]).GetProperty("isStuck").GetBoolean().ShouldBeTrue();
		Item(listed, seeded["stuckSummarizing"]).GetProperty("isStuck").GetBoolean().ShouldBeTrue();
		Item(listed, seeded["pending"]).GetProperty("isStuck").GetBoolean().ShouldBeFalse();
	}

	[Fact]
	public async Task GivenNoBearerToken_WhenPendingCountsAreRead_ThenApiRefuses()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.GetAsync(PendingCounts);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenReportsInEveryState_WhenPendingCountsAreRead_ThenPendingFailedAndStuckAreCounted()
	{
		// Given — the collection runs one test at a time, so the difference is
		// exactly what this test seeded: pending, failed, and the stuck pair, but
		// not the fresh, private, unpublished, published, or deleted (REQ-MOD-090).
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var before = await Counts(client);
		await Seed();

		// When
		var after = await Counts(client);

		// Then
		after.GetProperty("reportsNeedingAction").GetInt32()
			.ShouldBe(before.GetProperty("reportsNeedingAction").GetInt32() + 4);
		after.GetProperty("reportsNeedingAction").GetInt32().ShouldBe((await List(client, "needs-action")).Count);
	}

	[Fact]
	public async Task GivenSafetyOfficer_WhenPendingCountsAreRead_ThenNoTranslationCountIsGiven()
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var counts = await Counts(client);

		// Then
		counts.GetProperty("answersAwaitingTranslation").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Theory]
	[InlineData("published", "published")]
	[InlineData("private", "private")]
	[InlineData("unpublished", "private,unpublished")]
	[InlineData("summary-failed", "failed")]
	public async Task GivenAStatusFilter_WhenListed_ThenOnlyMatchingReportsOfMineAppear(string filter,
																					   string expected)
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		ArgumentNullException.ThrowIfNull(expected);

		// When
		var listed = await List(client, filter);

		// Then
		listed.Select(item => item.GetProperty("id").GetString()!)
			.Where(seeded.Ids.Contains)
			.ShouldBe(expected.Split(',').Select(name => seeded[name]));
	}

	[Fact]
	public async Task GivenAnUnknownFilter_WhenListed_ThenProblemNamesTheKnownFilters()
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.GetAsync(new Uri("/api/admin/reports?filter=everything", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("detail").GetString()!.ShouldContain("needs-action");
	}

	[Fact]
	public async Task GivenAPendingReport_WhenDetailIsRead_ThenAnswersSummaryAndAttachmentsAreSupplied()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{seeded["pending"]}", UriKind.Relative));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("pending");
		detail.GetProperty("language").GetString().ShouldBe("en-CA");
		detail.GetProperty("consent").GetBoolean().ShouldBeTrue();

		var answers = detail.GetProperty("answers").EnumerateArray().ToList();
		var pilot = answers.Single(answer => answer.GetProperty("questionKey").GetString() == seeded.PilotKey);
		pilot.GetProperty("isPrivate").GetBoolean().ShouldBeTrue();
		pilot.GetProperty("labelEn").GetString().ShouldBe("Pilot name");
		pilot.GetProperty("labelFr").GetString().ShouldBe("Nom du pilote");
		pilot.GetProperty("values")[0].GetProperty("value").GetString().ShouldBe(Seeding.PilotName);
		pilot.GetProperty("type").GetString().ShouldBe("short_text");

		var narrative = answers.Single(answer => answer.GetProperty("questionKey").GetString() == seeded.NarrativeKey);
		narrative.GetProperty("isPrivate").GetBoolean().ShouldBeFalse();
		narrative.GetProperty("type").GetString().ShouldBe("long_text");
		narrative.GetProperty("values")[0].GetProperty("locale").GetString().ShouldBe("en-CA");

		var skipped = answers.Single(answer => answer.GetProperty("questionKey").GetString() == seeded.SkippedKey);
		skipped.GetProperty("values").GetArrayLength().ShouldBe(0);

		var summary = detail.GetProperty("summary");
		summary.GetProperty("aiSummaryEn").GetString().ShouldBe(Seeding.SummaryEn);
		summary.GetProperty("aiSummaryFr").GetString().ShouldBe(Seeding.SummaryFr);
		summary.GetProperty("model").GetString().ShouldBe("gemini-3.7-flash");
		summary.GetProperty("promptVersion").GetString().ShouldBe("summarize-anonymize.v3");
		summary.GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.Null);

		var attachments = detail.GetProperty("attachments").EnumerateArray().ToList();
		attachments.Select(file => (file.GetProperty("kind").GetString(), file.GetProperty("state").GetString()))
			.OrderBy(pair => pair.Item1)
			.ShouldBe([("document", "ready"), ("image", "processing"), ("video", "failed")]);
	}

	[Fact]
	public async Task GivenAReportWithAttachments_WhenDetailIsRead_ThenNoStorageKeyOrLinkIsSupplied()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{seeded["pending"]}", UriKind.Relative));
		var body = await response.Content.ReadAsStringAsync();

		// Then
		body.ShouldNotContain(seeded["pending"] + "/original/");
		body.ShouldNotContain("http", Case.Insensitive);
		body.ShouldNotContain("blobKey", Case.Insensitive);
		body.ShouldNotContain(Seeding.OriginalFileName);
	}

	[Fact]
	public async Task GivenAFailedReport_WhenDetailIsRead_ThenTheSafeErrorAndNoSummaryAreSupplied()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{seeded["failed"]}", UriKind.Relative));

		// Then
		detail.GetProperty("summaryError").GetString().ShouldBe(Seeding.SummaryError);
		detail.GetProperty("summary").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public async Task GivenAPublishedReport_WhenDetailIsRead_ThenApprovalProvenanceIsSupplied()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{seeded["published"]}", UriKind.Relative));

		// Then
		var summary = detail.GetProperty("summary");
		summary.GetProperty("approvedBySubject").GetString().ShouldBe(Seeding.ApproverSubject);
		summary.GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.String);
	}

	[Fact]
	public async Task GivenAReport_WhenDetailIsRead_ThenAContentFreeViewedRawReportRowIsWritten()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var reportId = TinyId.Parse(seeded["pending"]);

		// When
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog
			.Where(e => e.Action == AuditAction.ViewedRawReport && e.TargetId == reportId)
			.SingleAsync();

		entry.TargetType.ShouldBe("Report");
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
		(entry.Detail ?? string.Empty).ShouldNotContain(Seeding.PilotName);
	}

	[Theory]
	[InlineData("not-a-tiny-id")]
	[InlineData(null)]
	public async Task GivenAnUnknownOrMalformedId_WhenDetailIsRead_ThenApiReturns404(string? id)
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{id ?? TinyId.New().Value}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenADeletedReport_WhenDetailIsRead_ThenApiReturns404AndWritesNoAuditRow()
	{
		// Given
		var seeded = await Seed();
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var reportId = TinyId.Parse(seeded["deleted"]);

		// When
		using var response = await client.GetAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		(await database.AuditLog.AnyAsync(e => e.Action == AuditAction.ViewedRawReport && e.TargetId == reportId))
			.ShouldBeFalse();
	}

	private static JsonElement Item(IEnumerable<JsonElement> listed,
									string id)
	{
		return listed.Single(item => item.GetProperty("id").GetString() == id);
	}

	private static async Task<List<JsonElement>> List(HttpClient client,
													  string? filter)
	{
		var path = filter is null ? "/api/admin/reports" : $"/api/admin/reports?filter={filter}";
		var body = await client.GetFromJsonAsync<JsonElement>(new Uri(path, UriKind.Relative));
		return [.. body.EnumerateArray()];
	}

	private static readonly Uri PendingCounts = new("/api/admin/counts", UriKind.Relative);

	private static Task<JsonElement> Counts(HttpClient client)
	{
		return client.GetFromJsonAsync<JsonElement>(PendingCounts);
	}

	private async Task<Seeding.Seeded> Seed()
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await Seeding.ReportsInEveryState(database);
	}

	/// <summary>
	///     One report per workflow state, each submitted at a distinct time so their
	///     newest-first order is known. Timestamps sit in the future, so another test's
	///     reports in this shared database never interleave with these.
	/// </summary>
	private static class Seeding
	{
		public const string PilotName = "Avery Synthetic";
		public const string NarrativeText = "A synthetic narrative about a firm landing.";
		public const string SummaryEn = "The pilot made a firm landing.";
		public const string SummaryFr = "Le pilote a fait un atterrissage ferme.";
		public const string SummaryError = "The AI chat provider was unavailable for this summarization attempt.";
		public const string ApproverSubject = "synthetic-approver-subject";
		public const string OriginalFileName = "synthetic-evidence.pdf";

		public static async Task<Seeded> ReportsInEveryState(HpacSafetyDbContext database)
		{
			var now = DateTimeOffset.UtcNow;
			var consent = await ConsentQuestion(database, now);
			var suffix = Guid.NewGuid().ToString("n")[..8];

			var pilot = Question.Create($"pilot_{suffix}", QuestionType.ShortText, "Pilot name", "Nom du pilote", now, isPrivate: true);
			var narrative = Question.Create($"narrative_{suffix}", QuestionType.LongText, "What happened", "Ce qui s'est passé", now, isPrivate: false);
			var skipped = Question.Create($"skipped_{suffix}", QuestionType.ShortText, "Anything else", "Autre chose", now, isPrivate: false);
			database.Questions.AddRange(pilot, narrative, skipped);

			// Newest first: each step back in this list is one minute older. The
			// stuck pair is older than a day; everything else is in the future.
			var future = now.AddYears(5);
			var reports = new Dictionary<string, Report>(StringComparer.Ordinal);

			Report Add(string name,
					   DateTimeOffset submittedAt,
					   bool consentAnswer)
			{
				var report = new Report(Locale.EnCa, submittedAt);
				report.Answer(consent, consentAnswer, submittedAt);
				report.Answer(pilot, PilotName, submittedAt);
				report.Answer(narrative, NarrativeText, submittedAt);
				report.Answer(skipped, (string?)null, submittedAt);
				database.Reports.Add(report);
				reports[name] = report;
				return report;
			}

			var pending = Add("pending", future, true);
			Summarize(pending, now);
			pending.AddFile(TinyId.New(), $"{pending.Id}/original/doc", "application/pdf", 10, OriginalFileName, now);
			pending.AddFile(TinyId.New(), $"{pending.Id}/original/img", "image/jpeg", 10, "photo.jpg", now);
			pending.AddFile(TinyId.New(), $"{pending.Id}/original/vid", "video/mp4", 10, "clip.mp4", now)
				.RecordProcessingFailure("remux_failed");

			var unconsented = Add("private", future.AddMinutes(-1), false);
			unconsented.BeginSummarizing();
			unconsented.KeepUnpublished();

			var failed = Add("failed", future.AddMinutes(-2), true);
			failed.BeginSummarizing();
			failed.FailSummarization(SummaryError);

			var unpublished = Add("unpublished", future.AddMinutes(-3), true);
			Summarize(unpublished, now);
			unpublished.Unpublish();

			var published = Add("published", future.AddMinutes(-4), true);
			Summarize(published, now);
			published.Publish(ApproverSubject, now);

			Add("freshSummarizing", future.AddMinutes(-6), true).BeginSummarizing();

			var deleted = Add("deleted", future.AddMinutes(-7), true);
			deleted.SoftDelete(now);

			Add("stuckSubmitted", now.AddHours(-25), true);
			Add("stuckSummarizing", now.AddHours(-26), true).BeginSummarizing();

			await database.SaveChangesAsync();

			return new Seeded(
				reports.ToDictionary(pair => pair.Key, pair => pair.Value.Id.Value, StringComparer.Ordinal),
				pilot.Key,
				narrative.Key,
				skipped.Key);
		}

		private static void Summarize(Report report,
									  DateTimeOffset at)
		{
			report.BeginSummarizing();
			report.AttachSummary(Summary.Generate(report.Id, SummaryEn, SummaryFr, "gemini-3.7-flash", "summarize-anonymize.v3", at));
			report.AwaitReview();
		}

		private static async Task<Question> ConsentQuestion(HpacSafetyDbContext database,
															DateTimeOffset at)
		{
			var existing = await database.Questions
				.Include(question => question.Revisions)
				.SingleOrDefaultAsync(question => question.Key == QuestionKey.ConsentPublish);

			if (existing is not null)
			{
				return existing;
			}

			var consent = Question.CreateConsentPublish(
				"May we publish a de-identified version of your report?",
				"Pouvons-nous publier une version anonymisée de votre rapport ?",
				at);
			database.Questions.Add(consent);
			return consent;
		}

		public sealed record Seeded(
			IReadOnlyDictionary<string, string> ByName,
			string PilotKey,
			string NarrativeKey,
			string SkippedKey)
		{
			public string this[string name] => ByName[name];

			public HashSet<string> Ids => [.. ByName.Values];

			/// <summary>Every seeded live report, in the order the list must return them.</summary>
			public IReadOnlyList<string> LiveNewestFirst =>
			[
				this["pending"], this["private"], this["failed"], this["unpublished"], this["published"],
				this["freshSummarizing"], this["stuckSubmitted"], this["stuckSummarizing"],
			];
		}
	}
}
