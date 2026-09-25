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
///     The review commands against a real PostgreSQL container: authorization,
///     optimistic concurrency on xmin, state refusals, validation, and audit rows
///     (REQ-MOD-032..035, REQ-MOD-055..061, REQ-DOM-015, ADR-0105, ADR-0125). Every report is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ReportReviewCommandEndpointTests(ApiPostgresFixture fixture)
{
	private const string Note = "Synthetic note: not enough detail.";

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData("publish")]
	[InlineData("unpublish")]
	[InlineData("summary")]
	public async Task GivenNoBearerToken_WhenACommandIsSent_ThenApiRefuses(string command)
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await Send(client, TinyId.New().Value, command, new { version = "1.1" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Theory]
	[InlineData("publish")]
	[InlineData("summary")]
	public async Task GivenUserRole_WhenACommandIsSent_ThenApiForbids(string command)
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.User);

		// When
		using var response = await Send(client, id, command, new { version, aiSummaryEn = "en", aiSummaryFr = "fr" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenConsentedPendingReport_WhenPublished_ThenPublishedWithApproverAndOneAuditRow()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "publish", new { version }));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("published");
		detail.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.String);
		detail.GetProperty("summary").GetProperty("approvedBySubject").GetString().ShouldNotBeNullOrWhiteSpace();
		detail.GetProperty("version").GetString().ShouldNotBe(version);
		(await Audits(id)).ShouldBe([AuditAction.PublishedReport]);
	}

	[Fact]
	public async Task GivenConsentedUnpublishedReport_WhenPublished_ThenPublishedAgain()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Unpublished, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		var detail = await Ok(await Send(client, id, "publish", new { version }));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("published");
		detail.GetProperty("unpublishNote").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Theory]
	[InlineData("publish")]
	[InlineData("unpublish")]
	[InlineData("summary")]
	public async Task GivenReportWithoutConsent_WhenAnyReviewCommandIsSent_ThenInvalidTransitionAndNothingChanges(string command)
	{
		// Given — the Worker set it Unpublished for good (REQ-DOM-015)
		var (id, version) = await Seed(ReportStatus.Unpublished, "no");
		using var client = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var response = await Send(client, id, command, new { version, aiSummaryEn = "en", aiSummaryFr = "fr" });

		// Then
		await ProblemOf(response, HttpStatusCode.Conflict, "invalid-transition");
		(await Audits(id)).ShouldBeEmpty();
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{id}", UriKind.Relative));
		detail.GetProperty("status").GetString().ShouldBe("unpublished");
		detail.GetProperty("summary").ValueKind.ShouldBe(JsonValueKind.Null);
	}

	[Fact]
	public async Task GivenReportWithoutConsent_WhenDeleted_ThenDeletedAndAudited()
	{
		// Given
		var (id, _) = await Seed(ReportStatus.Unpublished, "no");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{id}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(id);
		(await database.AuditLog.AnyAsync(e => e.TargetId == reportId && e.Action == AuditAction.DeletedReport)).ShouldBeTrue();
	}

	[Fact]
	public async Task GivenPublishedReport_WhenPairIsEdited_ThenPendingApprovalClearedAndAudited()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Published, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "summary", new { version, aiSummaryEn = "The pilot landed firmly.", aiSummaryFr = "Le pilote s'est posé fermement." }));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("pending");
		detail.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		detail.GetProperty("summary").GetProperty("aiSummaryEn").GetString().ShouldBe("The pilot landed firmly.");
		detail.GetProperty("summary").GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		(await Audits(id)).ShouldBe([AuditAction.EditedSummary]);
	}

	[Fact]
	public async Task GivenGeneratedPair_WhenEnglishIsEditedAndFrenchTranslationAccepted_ThenSourcesAreHumanAndMachine()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "summary", new
		{
			version,
			aiSummaryEn = "The pilot landed firmly.",
			aiSummaryFr = "Le pilote s'est posé fermement.",
			sourceEn = "human",
			sourceFr = "machine",
		}));

		// Then
		detail.GetProperty("summary").GetProperty("sourceEn").GetString().ShouldBe("human");
		detail.GetProperty("summary").GetProperty("sourceFr").GetString().ShouldBe("machine");
	}

	[Fact]
	public async Task GivenGeneratedPair_WhenOnlyEnglishChangesWithNoSourcesSent_ThenEnglishHumanAndFrenchStillGenerated()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "summary", new { version, aiSummaryEn = "Changed.", aiSummaryFr = "Le pilote s'est posé." }));

		// Then
		detail.GetProperty("summary").GetProperty("sourceEn").GetString().ShouldBe("human");
		detail.GetProperty("summary").GetProperty("sourceFr").GetString().ShouldBe("generated");
	}

	[Theory]
	[InlineData("generated")]
	[InlineData("robot")]
	public async Task GivenSourceAReviewerCannotClaim_WhenPairIsSaved_ThenBadRequest(string source)
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await Send(client, id, "summary", new { version, aiSummaryEn = "Changed.", aiSummaryFr = "Changé.", sourceEn = source });

		// Then
		await ProblemOf(response, HttpStatusCode.BadRequest, "invalid-review");
		(await Audits(id)).ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenFailedReport_WhenPairIsSaved_ThenManualPairAndPending()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.SummaryFailed, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "summary", new { version, aiSummaryEn = "The pilot landed.", aiSummaryFr = "Le pilote s'est posé." }));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("pending");
		detail.GetProperty("summaryError").ValueKind.ShouldBe(JsonValueKind.Null);
		detail.GetProperty("summary").GetProperty("model").GetString().ShouldBe("manual");
		detail.GetProperty("summary").GetProperty("promptVersion").GetString().ShouldBe("manual");
	}

	[Fact]
	public async Task GivenPendingReport_WhenUnpublishedWithNote_ThenNoteShownAndAuditRowCarriesNoNote()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "unpublish", new { version, note = Note }));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("unpublished");
		detail.GetProperty("unpublishNote").GetString().ShouldBe(Note);
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(id);
		var entry = await database.AuditLog.SingleAsync(e => e.TargetId == reportId && e.Action == AuditAction.UnpublishedReport);
		(entry.Detail ?? string.Empty).ShouldNotContain(Note);
	}

	[Fact]
	public async Task GivenPublishedReport_WhenUnpublished_ThenUnpublishedApprovalClearedAndAudited()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Published, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		var detail = await Ok(await Send(client, id, "unpublish", new { version }));

		// Then
		detail.GetProperty("status").GetString().ShouldBe("unpublished");
		detail.GetProperty("publishedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		detail.GetProperty("summary").GetProperty("approvedAt").ValueKind.ShouldBe(JsonValueKind.Null);
		(await Audits(id)).ShouldBe([AuditAction.UnpublishedReport]);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not-a-version")]
	[InlineData("1.2.3")]
	[InlineData("1")]
	public async Task GivenMissingOrMalformedVersion_WhenACommandIsSent_ThenStaleAndNothingChanges(string? version)
	{
		// Given
		var (id, _) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await Send(client, id, "publish", new { version });

		// Then
		await ProblemOf(response, HttpStatusCode.Conflict, "stale-report");
		(await Audits(id)).ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenAnotherReviewerSavedFirst_WhenAStaleEditIsSent_ThenConflictAndTheFirstEditStands()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var first = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		using var second = await SignedInClient.As(_factory, MemberRole.Administrator);
		await Ok(await Send(first, id, "summary", new { version, aiSummaryEn = "First.", aiSummaryFr = "Premier." }));

		// When
		using var response = await Send(second, id, "summary", new { version, aiSummaryEn = "Second.", aiSummaryFr = "Second." });

		// Then
		await ProblemOf(response, HttpStatusCode.Conflict, "stale-report");
		var detail = await first.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{id}", UriKind.Relative));
		detail.GetProperty("summary").GetProperty("aiSummaryEn").GetString().ShouldBe("First.");
	}

	[Fact]
	public async Task GivenAReviewerPublishedFirst_WhenAStaleUnpublishIsSent_ThenConflict()
	{
		// Given — publishing changes the report row, not only the summary
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		await Ok(await Send(client, id, "publish", new { version }));

		// When
		using var response = await Send(client, id, "unpublish", new { version });

		// Then
		await ProblemOf(response, HttpStatusCode.Conflict, "stale-report");
	}

	[Theory]
	[InlineData(ReportStatus.Published, "publish")]
	[InlineData(ReportStatus.SummaryFailed, "publish")]
	[InlineData(ReportStatus.SummaryFailed, "unpublish")]
	[InlineData(ReportStatus.Unpublished, "unpublish")]
	public async Task GivenStateThatRefusesTheAction_WhenSent_ThenInvalidTransitionAndNoAuditRow(ReportStatus status,
																								  string command)
	{
		// Given
		var (id, version) = await Seed(status, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await Send(client, id, command, new { version, aiSummaryEn = "en", aiSummaryFr = "fr" });

		// Then
		var problem = await ProblemOf(response, HttpStatusCode.Conflict, "invalid-transition");
		problem.GetProperty("detail").GetString()!.ShouldContain(" cannot ");
		(await Audits(id)).ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenBlankText_WhenPairIsSaved_ThenBadRequestAndNothingChanges()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await Send(client, id, "summary", new { version, aiSummaryEn = "Text.", aiSummaryFr = "  " });

		// Then
		await ProblemOf(response, HttpStatusCode.BadRequest, "invalid-review");
		(await Audits(id)).ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenNoteLongerThanAllowed_WhenUnpublished_ThenBadRequest()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await Send(client, id, "unpublish", new { version, note = new string('x', Report.UnpublishNoteMaxLength + 1) });

		// Then
		await ProblemOf(response, HttpStatusCode.BadRequest, "invalid-review");
	}

	[Theory]
	[InlineData("not-a-tiny-id")]
	[InlineData(null)]
	public async Task GivenUnknownOrMalformedReport_WhenACommandIsSent_ThenNotFound(string? id)
	{
		// Given
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await Send(client, id ?? TinyId.New().Value, "publish", new { version = "1.1" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenDeletedReport_WhenACommandIsSent_ThenNotFound()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");
		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		(await client.DeleteAsync(new Uri($"/api/admin/reports/{id}", UriKind.Relative))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

		// When
		using var response = await Send(client, id, "publish", new { version });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenDetailRead_WhenVersionIsReturned_ThenItNamesBothRows()
	{
		// Given
		var (id, version) = await Seed(ReportStatus.Pending, "yes");

		// When
		var parts = version.Split('.');

		// Then
		parts.Length.ShouldBe(2);
		uint.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0u);
		uint.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture).ShouldBeGreaterThan(0u);
		id.ShouldNotBeNullOrWhiteSpace();
	}

	private static async Task<HttpResponseMessage> Send(HttpClient client,
														string id,
														string command,
														object body)
	{
		return command == "summary"
			? await client.PutAsJsonAsync($"/api/admin/reports/{id}/summary", body)
			: await client.PostAsJsonAsync($"/api/admin/reports/{id}/{command}", body);
	}

	private static async Task<JsonElement> Ok(HttpResponseMessage response)
	{
		using (response)
		{
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			return await response.Content.ReadFromJsonAsync<JsonElement>();
		}
	}

	private static async Task<JsonElement> ProblemOf(HttpResponseMessage response,
													 HttpStatusCode status,
													 string code)
	{
		response.StatusCode.ShouldBe(status);
		var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
		problem.GetProperty("type").GetString().ShouldBe($"https://hpac.ca/problems/{code}");
		return problem;
	}

	private async Task<List<AuditAction>> Audits(string id)
	{
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var reportId = TinyId.Parse(id);

		return await database.AuditLog
			.Where(entry => entry.TargetId == reportId && entry.Action != AuditAction.ViewedRawReport && entry.Action != AuditAction.DeletedReport)
			.Select(entry => entry.Action)
			.ToListAsync();
	}

	/// <summary>Seeds a report in <paramref name="status" /> and reads its detail for the version.</summary>
	private async Task<(string Id, string Version)> Seed(ReportStatus status,
														 string consent)
	{
		var now = DateTimeOffset.UtcNow;
		string id;

		await using (var scope = _factory.Services.CreateAsyncScope())
		{
			var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
			var consentQuestion = await database.Questions
				.Include(question => question.Revisions)
				.SingleOrDefaultAsync(question => question.Key == QuestionKey.ConsentPublish);

			if (consentQuestion is null)
			{
				consentQuestion = Question.CreateConsentPublish(
					"May we publish a de-identified version of your report?",
					"Pouvons-nous publier une version anonymisée de votre rapport ?",
					now);
				database.Questions.Add(consentQuestion);
			}

			var report = new Report(Locale.EnCa, now);
			report.Answer(consentQuestion, consent, now);
			report.BeginSummarizing();

			if (consent == "no")
			{
				report.KeepUnpublished();
			}
			else if (status == ReportStatus.SummaryFailed)
			{
				report.FailSummarization("The provider was unavailable.");
			}
			else
			{
				report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "gemini-3.7-flash", "summarize-anonymize.v3", now));
				report.AwaitReview();

				if (status == ReportStatus.Published)
				{
					report.Publish("synthetic-approver", now);
				}
				else if (status == ReportStatus.Unpublished)
				{
					report.Unpublish();
				}
			}

			report.Status.ShouldBe(status);
			database.Reports.Add(report);
			await database.SaveChangesAsync();
			id = report.Id.Value;
		}

		using var client = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		var detail = await client.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{id}", UriKind.Relative));
		return (id, detail.GetProperty("version").GetString()!);
	}
}
