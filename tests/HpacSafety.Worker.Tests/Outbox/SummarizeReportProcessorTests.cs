using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     Issue #17 against a real database: claiming a due summarization outbox
///     message (via <see cref="OutboxClaimer" />), building the eligible-fields
///     input, making the one call, and persisting the outcome.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SharedWorkerPostgres.Name)]
public sealed class SummarizeReportProcessorTests(WorkerPostgresFixture postgres)
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenSummarizeReportOutboxType_WhenAsked_ThenHandlesSummarizeReport()
	{
		// Given
		var processor = new SummarizeReportProcessor(null!, new FakeSummarizer(("en", "fr")), TimeProvider.System);

		// Then
		processor.HandlesType.ShouldBe(OutboxMessageType.SummarizeReport);
	}

	[Fact]
	public async Task GivenADueMessage_WhenClaimedAndProcessed_ThenTheReportMovesToPendingReviewWithOnePairRow()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var report = await Seed(context);
		var summarizer = new FakeSummarizer(("The pilot reported a hard landing.", "Le pilote a signalé un atterrissage brutal."));
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);

		// When
		var claimed = await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, At, processor.Process, CancellationToken.None);

		// Then
		claimed.ShouldBeTrue();
		summarizer.CallCount.ShouldBe(1);

		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var persistedReport = await reader.Reports.SingleAsync(r => r.Id == report.Id);
		persistedReport.Status.ShouldBe(ReportStatus.PendingReview);

		var summary = await reader.Summaries.SingleAsync(s => s.ReportId == report.Id);
		summary.AiSummaryEn.ShouldBe("The pilot reported a hard landing.");
		summary.AiSummaryFr.ShouldBe("Le pilote a signalé un atterrissage brutal.");
		summary.Model.ShouldBe("fixture-model");
		summary.PromptVersion.ShouldBe("fixture-v1");

		var message = await reader.OutboxMessages.SingleAsync(m => m.AggregateId == report.Id);
		message.IsProcessed.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenNoDueMessage_WhenClaimed_ThenNothingIsClaimedAndTheSummarizerIsNeverCalled()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var summarizer = new FakeSummarizer(("en", "fr"));
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);

		// When
		var claimed = await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, At, processor.Process, CancellationToken.None);

		// Then
		claimed.ShouldBeFalse();
		summarizer.CallCount.ShouldBe(0);
	}

	[Fact]
	public async Task GivenConsentSkippedAndFileUploadAnswers_WhenProcessed_ThenOnlyEligibleFieldsReachTheModel()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);

		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
		var narrative = Question.Create("narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isPrivate: false);
		var weather = Question.Create("weather", QuestionType.ShortText, "Weather", "Météo", At, isPrivate: false, isRequired: false);
		var photo = Question.Create("photo", QuestionType.FileUpload, "Photo", "Photo", At, isPrivate: false);
		context.Questions.AddRange(consent, narrative, weather, photo);
		await context.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		report.Answer(consent, ["yes"], At);
		report.Answer(narrative, "Rough landing in gusty wind.", At);
		report.Answer(weather, value: null, At);
		report.Answer(photo, "s3://irrelevant", At);
		report.EnsureReadyForSubmission();
		context.Reports.Add(report);
		context.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At));
		await context.SaveChangesAsync();

		var summarizer = new FakeSummarizer(("en", "fr"));
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);

		// When
		await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, At, processor.Process, CancellationToken.None);

		// Then
		var input = summarizer.LastInput.ShouldNotBeNull();
		input.ReportContent.Select(field => field.QuestionKey).ShouldBe(["narrative"]);
		input.PrivateContext.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenAPrivateAnswer_WhenProcessed_ThenItReachesOnlyPrivateContext()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var report = await Seed(context);

		var summarizer = new FakeSummarizer(("en", "fr"));
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);

		// When
		await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, At, processor.Process, CancellationToken.None);

		// Then
		var input = summarizer.LastInput.ShouldNotBeNull();
		input.PrivateContext.Select(field => field.QuestionKey).ShouldBe(["pilot_name"]);
		input.ReportContent.Select(field => field.QuestionKey).ShouldBe(["narrative"]);
		report.ShouldNotBeNull();
	}

	[Fact]
	public async Task GivenTheSummarizerFails_WhenProcessed_ThenTheFailureIsRecordedAndTheReportStaysInReview()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var report = await Seed(context);
		var summarizer = new FakeSummarizer(failing: true);
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);

		// When
		await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, At, processor.Process, CancellationToken.None);

		// Then
		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var persistedReport = await reader.Reports.SingleAsync(r => r.Id == report.Id);
		persistedReport.Status.ShouldBe(ReportStatus.Summarizing);
		persistedReport.SummaryError.ShouldBeNull();

		var message = await reader.OutboxMessages.SingleAsync(m => m.AggregateId == report.Id);
		message.Attempts.ShouldBe(1);
		message.IsPoisoned.ShouldBeFalse();
	}

	[Fact]
	public async Task GivenRetriesAreExhausted_WhenTheFinalAttemptFails_ThenTheReportBecomesSummaryFailed()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var report = await Seed(context);
		var summarizer = new FakeSummarizer(failing: true);
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);
		var now = At;

		// When — the outbox's own poison threshold
		for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++)
		{
			await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, now, processor.Process, CancellationToken.None);
			now = now.AddMinutes(10);
		}

		// Then
		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var persistedReport = await reader.Reports.SingleAsync(r => r.Id == report.Id);
		persistedReport.Status.ShouldBe(ReportStatus.SummaryFailed);
		persistedReport.SummaryError.ShouldNotBeNullOrWhiteSpace();
		persistedReport.SummaryError.ShouldNotContain("Ada Lovelace");

		var message = await reader.OutboxMessages.SingleAsync(m => m.AggregateId == report.Id);
		message.IsPoisoned.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenAReportAlreadyPastSummarization_WhenProcessed_ThenNothingChangesAndTheSummarizerIsNeverCalled()
	{
		// Given — some other path already produced a pair before this attempt ran
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var context = WorkerPostgresFixture.ContextFor(connectionString);
		var report = await Seed(context);
		report.BeginSummarizing();
		var summary = Summary.Generate(report.Id, "en", "fr", "m", "v", At);
		report.AttachSummary(summary);
		report.AwaitReview();
		context.Summaries.Add(summary);
		await context.SaveChangesAsync();

		var summarizer = new FakeSummarizer(failing: true);
		var processor = new SummarizeReportProcessor(context, summarizer, TimeProvider.System);

		// When
		var claimed = await OutboxClaimer.ClaimNext(context, OutboxMessageType.SummarizeReport, At, processor.Process, CancellationToken.None);

		// Then
		claimed.ShouldBeTrue();
		summarizer.CallCount.ShouldBe(0);

		await using var reader = WorkerPostgresFixture.ContextFor(connectionString);
		var message = await reader.OutboxMessages.SingleAsync(m => m.AggregateId == report.Id);
		message.IsProcessed.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenTwoConcurrentClaims_WhenBothClaimTheSameDueMessage_ThenOnlyOneSucceeds()
	{
		// Given
		var connectionString = await postgres.CreateMigratedDatabase();
		await using var seedContext = WorkerPostgresFixture.ContextFor(connectionString);
		await Seed(seedContext);

		await using var contextA = WorkerPostgresFixture.ContextFor(connectionString);
		await using var contextB = WorkerPostgresFixture.ContextFor(connectionString);
		var summarizerA = new FakeSummarizer(("en", "fr"));
		var summarizerB = new FakeSummarizer(("en", "fr"));
		var processorA = new SummarizeReportProcessor(contextA, summarizerA, TimeProvider.System);
		var processorB = new SummarizeReportProcessor(contextB, summarizerB, TimeProvider.System);

		// When
		var results = await Task.WhenAll(
			OutboxClaimer.ClaimNext(contextA, OutboxMessageType.SummarizeReport, At, processorA.Process, CancellationToken.None),
			OutboxClaimer.ClaimNext(contextB, OutboxMessageType.SummarizeReport, At, processorB.Process, CancellationToken.None));

		// Then — exactly one claim found work; the other found none
		results.ShouldContain(true);
		results.ShouldContain(false);
		(summarizerA.CallCount + summarizerB.CallCount).ShouldBe(1);
	}

	private static async Task<Report> Seed(HpacSafetyDbContext context)
	{
		var consent = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
		var pilotName = Question.Create("pilot_name", QuestionType.ShortText, "Pilot name", "Nom du pilote", At, isPrivate: true);
		var narrative = Question.Create("narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isPrivate: false);
		context.Questions.AddRange(consent, pilotName, narrative);
		await context.SaveChangesAsync();

		var report = new Report(Locale.EnCa, At);
		report.Answer(consent, ["yes"], At);
		report.Answer(pilotName, "Ada Lovelace", At);
		report.Answer(narrative, "Ada Lovelace reported a hard landing.", At);
		report.EnsureReadyForSubmission();

		context.Reports.Add(report);
		context.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At));
		await context.SaveChangesAsync();

		return report;
	}
}
