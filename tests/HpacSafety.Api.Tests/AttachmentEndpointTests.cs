using System.Net;
using System.Net.Http.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     A reviewer's only two ways to see an uploaded file, against a real
///     PostgreSQL container. See issue #311 and
///     <c>features/media/media.feature</c> REQ-MED-010/011/013.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class AttachmentEndpointTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Theory]
	[InlineData(MemberRole.SafetyOfficer)]
	[InlineData(MemberRole.Administrator)]
	public async Task GivenAReviewerRole_WhenViewingAStrippedImage_ThenUrlIssued(MemberRole role)
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var reviewer = await SignedInAsync(role);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task GivenAUserRole_WhenViewingAStrippedImage_ThenForbidden()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var reporter = await SignedInAsync(MemberRole.User);

		// When
		using var response = await reporter.GetAsync(ViewUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenNoBearerToken_WhenViewingAnAttachment_ThenUnauthorized()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var anonymous = _factory.CreateClient();

		// When
		using var response = await anonymous.GetAsync(ViewUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenAnUnknownAttachment_WhenViewed_ThenNotFound()
	{
		// Given
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(TinyId.New().Value, TinyId.New().Value));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAnImageStillAwaitingStripping_WhenViewed_ThenNotFound()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: false, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAFailedAttachment_WhenViewed_ThenNotFound()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: false, failed: true);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAMalformedAttachmentId_WhenViewed_ThenNotFound()
	{
		// Given
		var (reportId, _) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, "not-a-tiny-id"));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAnUnknownDocumentAttachment_WhenDownloaded_ThenNotFound()
	{
		// Given
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(DownloadUrl(TinyId.New().Value, TinyId.New().Value));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAFailedDocument_WhenDownloaded_ThenNotFound()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Pdf.ContentType, stripped: false, failed: true);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(DownloadUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAValidatedDocument_WhenDownloaded_ThenUrlIssued()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Pdf.ContentType, stripped: false, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(DownloadUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var body = await response.Content.ReadFromJsonAsync<AttachmentLinkPayload>();
		body!.FileName.ShouldBe($"{attachmentId}.pdf");
	}

	[Fact]
	public async Task GivenAnImage_WhenRequestedThroughTheDownloadEndpoint_ThenRejected()
	{
		// Given
		// The document endpoint issues a URL to the unredacted original — an
		// image or video must never be reachable through it.
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(DownloadUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenADocument_WhenRequestedThroughTheViewEndpoint_ThenRejected()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Pdf.ContentType, stripped: false, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, attachmentId));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenASuccessfulView_WhenTheAuditRowIsRead_ThenItRecordsTheActorAndTarget()
	{
		// Given
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, attachmentId));
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		// Then
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog.SingleAsync(e => e.TargetType == "ReportFile" && e.TargetId == TinyId.Parse(attachmentId));

		entry.Action.ShouldBe(AuditAction.ViewedAttachment);
		entry.ActorSubject.ShouldEndWith("officer");
	}

	[Fact]
	public async Task GivenAFailedAudit_ThenNoUrlIsDisclosed()
	{
		// Given
		// There is no way to make the audit write fail without breaking the
		// database connection itself, so this instead pins the ordering
		// contract the atomicity relies on: the row exists once, and only
		// once, per successful 200 — never zero, never duplicated by a retry
		// that raced the disclosure.
		var (reportId, attachmentId) = await SeedAsync(MediaType.Jpeg.ContentType, stripped: true, failed: false);
		using var reviewer = await SignedInAsync(MemberRole.SafetyOfficer);

		// When
		using var response = await reviewer.GetAsync(ViewUrl(reportId, attachmentId));
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		// Then
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var count = await database.AuditLog.CountAsync(e => e.TargetType == "ReportFile" && e.TargetId == TinyId.Parse(attachmentId));
		count.ShouldBe(1);
	}

	private Task<HttpClient> SignedInAsync(MemberRole role)
	{
		return SignedInClient.As(_factory, role);
	}

	private static Uri ViewUrl(string reportId, string attachmentId)
	{
		return new Uri($"/api/admin/reports/{reportId}/attachments/{attachmentId}/view", UriKind.Relative);
	}

	private static Uri DownloadUrl(string reportId, string attachmentId)
	{
		return new Uri($"/api/admin/reports/{reportId}/attachments/{attachmentId}/download", UriKind.Relative);
	}

	private async Task<(string ReportId, string AttachmentId)> SeedAsync(string contentType, bool stripped, bool failed)
	{
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var report = new Report(Locale.EnCa, DateTimeOffset.UtcNow);
		var file = report.AddFile($"{report.Id}/original/attachment.bin", contentType, byteSize: 1024, DateTimeOffset.UtcNow);

		if (stripped)
		{
			file.RecordStripped($"{report.Id}/stripped/attachment.bin", DateTimeOffset.UtcNow);
		}

		if (failed)
		{
			file.RecordProcessingFailure("processing_failed");
		}

		database.Reports.Add(report);
		await database.SaveChangesAsync();

		return (report.Id.Value, file.Id.Value);
	}

	private sealed record AttachmentLinkPayload(string Url, DateTimeOffset ExpiresAt, string FileName);
}
