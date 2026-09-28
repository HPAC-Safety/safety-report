using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Worker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     The API nudges the Worker exactly when a save actually queued outbox
///     work, and never fails the request when the nudge itself fails
///     (ADR-0123, ADR-0159, issue #443).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedApiPostgres.Name)]
public sealed class WorkerNudgeTests(ApiPostgresFixture fixture)
{
	private readonly WebApplicationFactory<Program> _factory = fixture.Factory;

	[Fact]
	public async Task GivenACommentIsPosted_WhenItsTranslationIsQueued_ThenTheWorkerIsNudgedOnce()
	{
		// Given
		var nudge = new CountingWorkerNudge();
		await using var nudged = WithNudge(nudge);
		var reportId = await Published(nudged);
		using var member = await SignedInClient.As(nudged, MemberRole.User);

		// When
		using var response = await member.PostAsJsonAsync(
			$"/api/v1/public/reports/{reportId}/comments", new { text = "Synthetic.", locale = "en-CA" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		nudge.Calls.ShouldBe(1);
	}

	[Fact]
	public async Task GivenACommentIsRejectedBeforeItIsSaved_WhenPosted_ThenTheWorkerIsNotNudged()
	{
		// Given — an unknown locale never reaches a save at all.
		var nudge = new CountingWorkerNudge();
		await using var nudged = WithNudge(nudge);
		var reportId = await Published(nudged);
		using var member = await SignedInClient.As(nudged, MemberRole.User);

		// When
		using var response = await member.PostAsJsonAsync(
			$"/api/v1/public/reports/{reportId}/comments", new { text = "Synthetic.", locale = "de-DE" });

		// Then
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		nudge.Calls.ShouldBe(0);
	}

	[Fact]
	public async Task GivenAFailingNudge_WhenACommentIsPosted_ThenTheRequestStillSucceeds()
	{
		// Given
		await using var nudged = WithNudge(new ThrowingWorkerNudge());
		var reportId = await Published(nudged);
		using var member = await SignedInClient.As(nudged, MemberRole.User);

		// When
		using var response = await member.PostAsJsonAsync(
			$"/api/v1/public/reports/{reportId}/comments", new { text = "Synthetic.", locale = "en-CA" });

		// Then — a failed nudge never fails the request that queued the work.
		response.StatusCode.ShouldBe(HttpStatusCode.Created);
	}

	private WebApplicationFactory<Program> WithNudge(IWorkerNudge nudge)
	{
		return _factory.WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				services.RemoveAll<IWorkerNudge>();
				services.AddSingleton(nudge);
			}));
	}

	private static async Task<string> Published(WebApplicationFactory<Program> factory)
	{
		await using var scope = factory.Services.CreateAsyncScope();
		var database = scope.ServiceProvider.GetRequiredService<HpacSafetyDbContext>();
		var now = DateTimeOffset.UtcNow;
		var consent = await database.Questions
						  .Include(question => question.Revisions)
						  .SingleOrDefaultAsync(question => question.Role == QuestionRole.ConsentPublish)
					  ?? database.Questions.Add(Question.CreateConsentPublish(
						  "May we publish a de-identified version of your report?",
						  "Pouvons-nous publier une version anonymisée de votre rapport ?",
						  now)).Entity;

		var report = new Report(Locale.EnCa, now);
		report.Answer(consent, true, now);
		report.BeginSummarizing();
		report.AttachSummary(Summary.Generate(report.Id, "The pilot landed.", "Le pilote s'est posé.", "synthetic-model", "synthetic.v1", now));
		report.AwaitReview();
		report.Publish("synthetic-approver", now);
		database.Reports.Add(report);
		await database.SaveChangesAsync();
		return report.Id.Value;
	}

	private sealed class CountingWorkerNudge : IWorkerNudge
	{
		public int Calls { get; private set; }

		public Task NudgeAsync(CancellationToken cancellationToken)
		{
			Calls++;
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingWorkerNudge : IWorkerNudge
	{
		public Task NudgeAsync(CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Synthetic nudge failure.");
		}
	}
}
