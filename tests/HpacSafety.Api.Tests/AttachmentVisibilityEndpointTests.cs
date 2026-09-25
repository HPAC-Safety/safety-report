using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
///     A reviewer hiding and showing a published report's image or video, and the
///     admin report view saying whether the public sees each file (ADR-0117,
///     REQ-MED-030, REQ-MED-036), against a real PostgreSQL container.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AttachmentVisibilityEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenImageHiddenTwice_WhenAudited_ThenOneHideIsRecorded()
	{
		// Given
		var (reportId, fileIds) = await Seed(ReportStatus.Published, mediaConsent: true, MediaType.Jpeg);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var first = await officer.PostAsync(Action(reportId, fileIds[0], "hide"), null);
		using var second = await officer.PostAsync(Action(reportId, fileIds[0], "hide"), null);

		// Then
		first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await AuditCount(fileIds[0], AuditAction.HidMedia)).ShouldBe(1);
	}

	[Fact]
	public async Task GivenVisibleImage_WhenShown_ThenNothingIsRecorded()
	{
		// Given
		var (reportId, fileIds) = await Seed(ReportStatus.Published, mediaConsent: true, MediaType.Jpeg);
		using var officer = await SignedInClient.As(_factory, MemberRole.Administrator);

		// When
		using var response = await officer.PostAsync(Action(reportId, fileIds[0], "show"), null);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
		(await AuditCount(fileIds[0], AuditAction.ShowedMedia)).ShouldBe(0);
	}

	[Fact]
	public async Task GivenDocument_WhenHidden_ThenNoContent()
	{
		// Given — a public document is moderated like a photo (ADR-0119)
		var (reportId, fileIds) = await Seed(ReportStatus.Published, mediaConsent: true, MediaType.Pdf);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await officer.PostAsync(Action(reportId, fileIds[0], "hide"), null);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
	}

	[Fact]
	public async Task GivenUnknownFile_WhenHidden_ThenNotFound()
	{
		// Given
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await officer.PostAsync(Action(TinyId.New().Value, TinyId.New().Value, "hide"), null);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenNoToken_WhenHidden_ThenUnauthorized()
	{
		// Given
		using var anonymous = _factory.CreateClient();

		// When
		using var response = await anonymous.PostAsync(Action(TinyId.New().Value, TinyId.New().Value, "hide"), null);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Theory]
	[InlineData(ReportStatus.Published, true, "public")]
	[InlineData(ReportStatus.Pending, true, "when_published")]
	[InlineData(ReportStatus.Published, false, "no_consent")]
	[InlineData(ReportStatus.Published, null, "no_consent")]
	public async Task GivenProcessedImage_WhenAdminReadsReport_ThenVisibilityFollowsConsentAndStatus(ReportStatus status,
		bool? mediaConsent,
		string expected)
	{
		// Given
		var (reportId, _) = await Seed(status, mediaConsent, MediaType.Jpeg);

		// When
		var detail = await Detail(reportId);

		// Then
		var consent = detail.GetProperty("mediaConsent");
		(consent.ValueKind == JsonValueKind.Null ? (bool?)null : consent.GetBoolean()).ShouldBe(mediaConsent);
		Visibilities(detail).ShouldBe([expected]);
	}

	[Fact]
	public async Task GivenHiddenDocumentAndUnprocessedFiles_WhenAdminReadsReport_ThenEachReadsAsNotPublic()
	{
		// Given
		var (reportId, fileIds) = await Seed(ReportStatus.Published, true, MediaType.Jpeg, MediaType.Pdf);
		await Unprocessed(reportId);
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		using var hid = await officer.PostAsync(Action(reportId, fileIds[0], "hide"), null);

		// When
		var detail = await Detail(reportId);

		// Then
		Visibilities(detail).ShouldBe(["hidden", "private", "private"], ignoreOrder: true);
	}

	private async Task<(string ReportId, List<string> FileIds)> Seed(ReportStatus status,
																	 bool? mediaConsent,
																	 params MediaType[] types)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var consent = await database.Questions.Include(question => question.Revisions)
						  .FirstOrDefaultAsync(question => question.Role == QuestionRole.ConsentPublish)
					  ?? Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Now);

		if (database.Entry(consent).State == EntityState.Detached)
		{
			database.Questions.Add(consent);
		}

		var media = await database.Questions.Include(question => question.Revisions)
			.SingleAsync(question => question.Key == QuestionKey.ConsentMedia);

		var report = new Report(Locale.EnCa, Now);
		report.Answer(consent, true, Now);

		if (mediaConsent is not null)
		{
			report.Answer(media, mediaConsent.Value, Now);
		}

		var ids = new List<string>();

		foreach (var type in types)
		{
			var fileId = TinyId.New();
			var file = report.AddFile(fileId, $"{report.Id}/original/{fileId}", type.ContentType, 10, null, Now);

			if (type.Kind is not MediaKind.Document)
			{
				file.RecordStripped($"{report.Id}/stripped/{fileId}", Now);
			}

			ids.Add(fileId.Value);
		}

		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote a atterri.", "gemini", "v1", Now));
		report.AwaitReview();

		if (status is ReportStatus.Published)
		{
			report.Publish("synthetic-approver", Now);
		}

		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return (report.Id.Value, ids);
	}

	private async Task Unprocessed(string reportId)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports.Include(candidate => candidate.Files).SingleAsync(candidate => candidate.Id == TinyId.Parse(reportId));
		var fileId = TinyId.New();
		report.AddFile(fileId, $"{report.Id}/original/{fileId}", MediaType.Jpeg.ContentType, 10, null, Now);
		await database.SaveChangesAsync();
	}

	private async Task<JsonElement> Detail(string reportId)
	{
		using var officer = await SignedInClient.As(_factory, MemberRole.SafetyOfficer);
		return await officer.GetFromJsonAsync<JsonElement>(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));
	}

	private static List<string> Visibilities(JsonElement detail)
	{
		return [.. detail.GetProperty("attachments").EnumerateArray().Select(item => item.GetProperty("visibility").GetString()!)];
	}

	private async Task<int> AuditCount(string fileId,
									   AuditAction action)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		return await database.AuditLog.CountAsync(entry => entry.Action == action && entry.TargetId == TinyId.Parse(fileId));
	}

	private static Uri Action(string reportId,
							  string fileId,
							  string action)
	{
		return new Uri($"/api/admin/reports/{reportId}/attachments/{fileId}/{action}", UriKind.Relative);
	}
}
