using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The edges of the receipt endpoints (#820, ADR-0196), against a real PostgreSQL
///     container. What a holder sees, and what nobody else does, is proven by the
///     REQ-PUB-001 to REQ-PUB-009 acceptance scenarios; these cover malformed
///     input, where a receipt may travel, and what the database refuses.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public class OwnReportEndpointTests(ApiPostgresFixture fixture)
{
	private const string Own = "/api/v1/public/reports/own";

	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenNoBody_WhenLookingUp_ThenBadRequest()
	{
		// Given
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(new Uri(Own, UriKind.Relative), new { }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenMoreReceiptsThanTheCap_WhenLookingUp_ThenBadRequest()
	{
		// Given
		using var client = _factory.CreateClient();
		var receipts = Enumerable.Range(0, 51).Select(_ => new { reportId = TinyId.New().Value, receipt = BrowserReceipt.New().Receipt });

		// When
		using var response = await client.PostAsJsonAsync(new Uri(Own, UriKind.Relative), new { receipts }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GivenMalformedEntries_WhenLookingUp_ThenEachIsSettledAndNothingIsListed()
	{
		// Given
		using var client = _factory.CreateClient();
		var receipts = new object?[]
		{
			new { reportId = "not-an-id", receipt = BrowserReceipt.New().Receipt },
			new { reportId = TinyId.New().Value, receipt = "short" },
			new { reportId = (string?)null, receipt = (string?)null },
			null,
		};

		// When
		using var response = await client.PostAsJsonAsync(new Uri(Own, UriKind.Relative), new { receipts }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		body.GetProperty("items").GetArrayLength().ShouldBe(0);
		body.GetProperty("settled").GetArrayLength().ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task GivenAHolder_WhenLookingUp_ThenTheAnswerIsNeverCached()
	{
		// Given
		var (id, receipt) = await SeedOwnReport();
		using var client = _factory.CreateClient();

		// When
		using var lookup = await client.PostAsJsonAsync(new Uri(Own, UriKind.Relative), new { receipts = new[] { new { reportId = id, receipt } } }, cancellationToken: TestContext.Current.CancellationToken);
		using var page = await client.PostAsJsonAsync(new Uri($"{Own}/{id}", UriKind.Relative), new { receipt }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		lookup.StatusCode.ShouldBe(HttpStatusCode.OK);
		page.StatusCode.ShouldBe(HttpStatusCode.OK);
		lookup.Headers.CacheControl!.NoStore.ShouldBeTrue();
		page.Headers.CacheControl!.NoStore.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenAReceiptInTheQueryString_WhenAskingForThePage_ThenItIsNotRead()
	{
		// Given
		var (id, receipt) = await SeedOwnReport();
		using var client = _factory.CreateClient();

		// When
		using var post = await client.PostAsync(new Uri($"{Own}/{id}?receipt={receipt}", UriKind.Relative), null, TestContext.Current.CancellationToken);
		using var get = await client.GetAsync(new Uri($"{Own}/{id}?receipt={receipt}", UriKind.Relative), TestContext.Current.CancellationToken);

		// Then
		post.StatusCode.ShouldBe(HttpStatusCode.NotFound);
		get.IsSuccessStatusCode.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenTwoReportsWithOneReceiptHash_WhenSaving_ThenTheDatabaseRefusesTheSecond()
	{
		// Given
		var hash = BrowserReceipt.New().Hash;
		await SeedOwnReport(hash);

		// When
		var second = async () => await SeedOwnReport(hash);

		// Then
		await second.ShouldThrowAsync<DbUpdateException>();
	}

	[Fact]
	public async Task GivenAStoredReceiptHash_WhenItIsChanged_ThenTheDatabaseRefuses()
	{
		// Given
		var (id, _) = await SeedOwnReport();
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		// When
		var change = async () => await database.Database.ExecuteSqlAsync($"UPDATE reports SET receipt_hash = {BrowserReceipt.New().Hash} WHERE id = {id}");

		// Then
		var refused = await change.ShouldThrowAsync<PostgresException>();
		refused.MessageText.ShouldContain("receipt_hash");
	}

	[Fact]
	public async Task GivenAReportThatWasPublishedAndUnpublished_WhenLookingUp_ThenItIsSettledAndFirstPublishedAtStays()
	{
		// Given
		var (id, receipt) = await SeedOwnReport(publishThenUnpublish: true);
		using var client = _factory.CreateClient();

		// When
		using var response = await client.PostAsJsonAsync(new Uri(Own, UriKind.Relative), new { receipts = new[] { new { reportId = id, receipt } } }, cancellationToken: TestContext.Current.CancellationToken);

		// Then
		var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
		body.GetProperty("items").GetArrayLength().ShouldBe(0);
		body.GetProperty("settled").EnumerateArray().Select(item => item.GetString()).ShouldBe([id]);

		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var report = await database.Reports.AsNoTracking().SingleAsync(candidate => candidate.Id == TinyId.Parse(id), cancellationToken: TestContext.Current.CancellationToken);
		report.PublishedAt.ShouldBeNull();
		report.FirstPublishedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenAFirstPublishedAt_WhenItIsChangedOrCleared_ThenTheDatabaseRefuses()
	{
		// Given
		var (id, _) = await SeedOwnReport(publishThenUnpublish: true);
		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();

		// When
		var clear = async () => await database.Database.ExecuteSqlAsync($"UPDATE reports SET first_published_at = NULL WHERE id = {id}");
		var change = async () => await database.Database.ExecuteSqlAsync($"UPDATE reports SET first_published_at = now() WHERE id = {id}");

		// Then
		(await clear.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("first_published_at");
		(await change.ShouldThrowAsync<PostgresException>()).MessageText.ShouldContain("first_published_at");
	}

	private async Task<(string Id, string Receipt)> SeedOwnReport(string? hash = null, bool publishThenUnpublish = false)
	{
		var (receipt, receiptHash) = BrowserReceipt.New();

		await using var scope = _factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = DateTimeOffset.UtcNow;

		// The shared database may not have the consent question yet; the same
		// find-or-create the other endpoint tests use.
		var consent = await database.Questions
						  .Include(question => question.Revisions)
						  .SingleOrDefaultAsync(question => question.Role == QuestionRole.ConsentPublish)
					  ?? database.Questions.Add(Question.CreateConsentPublish(
						  "May we publish a de-identified version of your report?",
						  "Pouvons-nous publier une version anonymisée de votre rapport ?",
						  now)).Entity;

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		report.AttachReceipt(hash ?? receiptHash);

		if (publishThenUnpublish)
		{
			report.BeginSummarizing();
			report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "synthetic-model", "synthetic.v1", now));
			report.AwaitReview();
			report.Publish("synthetic-approver", now);
			report.Unpublish();
		}

		database.Reports.Add(report);
		await database.SaveChangesAsync();

		return (report.Id.Value, receipt);
	}
}
