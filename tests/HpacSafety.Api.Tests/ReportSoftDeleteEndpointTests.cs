using System.Net;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     Soft-deleting a report, against a real PostgreSQL container — REQ-DOM-007,
///     issue #82. Every report here is synthetic.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class ReportSoftDeleteEndpointTests(ApiPostgresFixture fixture)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBearerToken_WhenReportIsDeleted_ThenApiRefuses()
	{
		// Given
		var reportId = await SeedReportAsync();
		using var client = _factory.CreateClient();

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GivenUserRole_WhenReportIsDeleted_ThenApiForbids()
	{
		// Given
		var reportId = await SeedReportAsync();
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.User);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then — signed in, and it is still not theirs
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GivenSafetyOfficer_WhenReportIsDeleted_ThenEveryOwnedRowAndPendingOutboxShareOneTimestampAndAreHidden()
	{
		// Given
		var (reportId, answerId, outboxId) = await SeedReportWithOutboxAsync();
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		// Hidden from every normal, live-filtered query
		(await database.Reports.AnyAsync(r => r.Id == reportId)).ShouldBeFalse();
		(await database.ReportAnswers.AnyAsync(a => a.Id == answerId)).ShouldBeFalse();
		(await database.OutboxMessages.AnyAsync(m => m.Id == outboxId)).ShouldBeFalse();

		// One shared timestamp, past the filter
		var report = await database.Reports.IgnoreQueryFilters().SingleAsync(r => r.Id == reportId);
		var answer = await database.ReportAnswers.IgnoreQueryFilters().SingleAsync(a => a.Id == answerId);
		var outboxMessage = await database.OutboxMessages.IgnoreQueryFilters().SingleAsync(m => m.Id == outboxId);
		report.Deleted.ShouldNotBeNull();
		answer.Deleted.ShouldBe(report.Deleted);
		outboxMessage.Deleted.ShouldBe(report.Deleted);
	}

	[Fact]
	public async Task GivenSafetyOfficer_WhenReportIsDeleted_ThenAContentFreeAuditRowIsWritten()
	{
		// Given
		var reportId = await SeedReportAsync();
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));
		response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

		// Then
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var entry = await database.AuditLog
			.Where(e => e.Action == AuditAction.DeletedReport && e.TargetId == reportId)
			.SingleAsync();

		entry.TargetType.ShouldBe("Report");
		entry.ActorSubject.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task GivenAnUnknownReportId_WhenDeleted_ThenApiReturns404()
	{
		// Given
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{TinyId.New()}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAMalformedReportId_WhenDeleted_ThenApiReturns404()
	{
		// Given
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.SafetyOfficer);

		// When
		using var response = await client.DeleteAsync(new Uri("/api/admin/reports/not-a-tiny-id", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task GivenAnAlreadyDeletedReport_WhenDeletedAgain_ThenApiReturns404()
	{
		// Given — soft-deleted reports are excluded from the endpoint's own query
		// too, the same as every other live-row read
		var reportId = await SeedReportAsync();
		using var client = await SignedInClient.AsAsync(_factory, MemberRole.SafetyOfficer);
		await client.DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// When
		using var response = await client.DeleteAsync(new Uri($"/api/admin/reports/{reportId}", UriKind.Relative));

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	private async Task<TinyId> SeedReportAsync()
	{
		var (reportId, _, _) = await SeedReportWithOutboxAsync();
		return reportId;
	}

	private async Task<(TinyId ReportId, TinyId AnswerId, TinyId OutboxId)> SeedReportWithOutboxAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		var question = Question.Create(
			$"synthetic_{Guid.NewGuid():n}"[..24], QuestionType.ShortText, "A synthetic question", "Une question synthétique", At);
		database.Questions.Add(question);

		var report = new Report(Locale.EnCa, At);
		var answer = report.Answer(question, "A synthetic answer.", At);
		database.Reports.Add(report);

		var outboxMessage = new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At);
		database.OutboxMessages.Add(outboxMessage);

		await database.SaveChangesAsync();

		return (report.Id, answer.Id, outboxMessage.Id);
	}
}
