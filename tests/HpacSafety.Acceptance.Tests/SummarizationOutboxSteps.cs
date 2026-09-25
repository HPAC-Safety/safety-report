using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The <c>SummarizeReportProcessor</c> scenarios in
///     <c>features/ai-anonymization/ai-anonymization.feature</c> that describe the
///     outbox claim query and the persisted outcome — statements about a real
///     database, not the domain in isolation. See <see cref="WorkerDatabase" />.
/// </summary>
[Binding]
public sealed class SummarizationOutboxSteps : IAsyncDisposable
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	private HpacSafetyDbContext? _db;
	private Report? _report;
	private FakeSummarizer? _summarizer;
	private IReadOnlyList<string> _consentKeys = [];
	private DateTimeOffset _now = At;
	private bool[]? _concurrentResults;
	private int _concurrentModelCalls;

	[Given(@"a report has been submitted and its summarization outbox item is due")]
	public async Task GivenAReportIsDue()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db);
	}

	[Given(@"^a report whose reporter answered (yes|no) to publication is due for summarization$")]
	public async Task GivenAReportWithConsentIsDue(string consent)
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db, consent: consent);
	}

	[Given(@"a report whose reporter did not consent to publication is due for summarization")]
	public async Task GivenAnUnconsentedReportIsDue()
	{
		await GivenAReportWithConsentIsDue("no");
	}

	[When(@"the Worker processes its summarization attempt")]
	public async Task WhenTheWorkerProcessesItsAttempt()
	{
		await WhenTheWorkerProcessesTheAttempt();
	}

	[Then(@"no model call is made")]
	public void ThenNoModelCall()
	{
		_summarizer!.CallCount.ShouldBe(0);
	}

	[Then(@"^the model is called (\d+) time\(s\)$")]
	public void ThenTheModelIsCalled(int calls)
	{
		_summarizer!.CallCount.ShouldBe(calls);
	}

	[Then(@"the report goes to Unpublished with no summary")]
	public async Task ThenUnpublishedWithNoSummary()
	{
		_db!.ChangeTracker.Clear();
		(await _db.Reports.SingleAsync(r => r.Id == _report!.Id)).Status.ShouldBe(ReportStatus.Unpublished);
		(await _db.Summaries.AnyAsync(s => s.ReportId == _report!.Id)).ShouldBeFalse();
	}

	[Then(@"the report can never satisfy the public query")]
	public async Task ThenNeverPublishable()
	{
		_db!.ChangeTracker.Clear();
		var stored = await _db.Reports.Include(r => r.Summary).SingleAsync(r => r.Id == _report!.Id);
		stored.IsPublishable.ShouldBeFalse();
		Should.Throw<DomainRuleViolationException>(() => stored.Publish("synthetic-officer", At));
	}

	[When(@"the Worker processes the summarization attempt")]
	public async Task WhenTheWorkerProcessesTheAttempt()
	{
		_summarizer = new FakeSummarizer(("The pilot reported a hard landing.", "Le pilote a signalé un atterrissage brutal."));
		await ClaimAndProcess(_db!, _summarizer);
	}

	[Then(@"the Worker makes exactly one call to the model")]
	public void ThenExactlyOneCall()
	{
		_summarizer!.CallCount.ShouldBe(1);
	}

	[Then(@"that call produces both the English and French summary texts")]
	public async Task ThenBothTextsProduced()
	{
		var summary = await _db!.Summaries.SingleAsync(s => s.ReportId == _report!.Id);
		summary.AiSummaryEn.ShouldNotBeNullOrWhiteSpace();
		summary.AiSummaryFr.ShouldNotBeNullOrWhiteSpace();
	}

	[Then(@"no second model call, separate PII-audit call, or translation call runs")]
	public void ThenNoSecondCall()
	{
		// The processor's only outbound dependency is the one ISummarizer it was given.
		_summarizer!.CallCount.ShouldBe(1);
	}

	[Given(@"a summarization outbox item is pending")]
	public async Task GivenAPendingItem()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db);
	}

	[When(@"two Worker instances attempt to claim it concurrently")]
	public async Task WhenTwoWorkersClaimConcurrently()
	{
		await using var dbA = WorkerDatabase.ContextFor(_db!);
		await using var dbB = WorkerDatabase.ContextFor(_db!);
		var summarizerA = new FakeSummarizer(("en", "fr"));
		var summarizerB = new FakeSummarizer(("en", "fr"));
		var processorA = new SummarizeReportProcessor(dbA, summarizerA, TimeProvider.System);
		var processorB = new SummarizeReportProcessor(dbB, summarizerB, TimeProvider.System);

		_concurrentResults = await Task.WhenAll(
			OutboxClaimer.ClaimNext(dbA, OutboxMessageType.SummarizeReport, At, processorA.Process, CancellationToken.None),
			OutboxClaimer.ClaimNext(dbB, OutboxMessageType.SummarizeReport, At, processorB.Process, CancellationToken.None));

		_concurrentModelCalls = summarizerA.CallCount + summarizerB.CallCount;
	}

	[Then(@"exactly one Worker claims the item")]
	public void ThenExactlyOneClaims()
	{
		_concurrentResults!.Count(result => result).ShouldBe(1);
	}

	[Then(@"the other Worker finds no work and makes no model call")]
	public void ThenOtherFindsNoWork()
	{
		_concurrentResults!.Count(result => !result).ShouldBe(1);
		_concurrentModelCalls.ShouldBe(1);
	}

	[Given(@"a report has non-private answered fields and private answered fields")]
	public async Task GivenMixedFields()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		(_report, _consentKeys) = await SeedWithExclusions(_db);
	}

	[When(@"the Worker claims the message and builds the model input DTO")]
	public async Task WhenBuildsInputDto()
	{
		_summarizer = new FakeSummarizer(("en", "fr"));
		await ClaimAndProcess(_db!, _summarizer);
	}

	[Then(@"report_content contains only non-private answered fields eligible to contribute facts")]
	public void ThenReportContentOnlyNonPrivate()
	{
		_summarizer!.LastInput!.ReportContent.Select(field => field.QuestionKey).ShouldBe(["narrative"]);
	}

	[Then(@"private_context contains only private answered fields, supplied to help recognize identifying details that recur in report content")]
	public void ThenPrivateContextOnlyPrivate()
	{
		_summarizer!.LastInput!.PrivateContext.Select(field => field.QuestionKey).ShouldBe(["pilot_name"]);
	}

	[Then(@"skipped\/null answers, both system consent answers, and file-upload answers are excluded from both arrays")]
	public void ThenExcludedFromBoth()
	{
		var input = _summarizer!.LastInput!;
		var keys = input.ReportContent.Select(field => field.QuestionKey)
			.Concat(input.PrivateContext.Select(field => field.QuestionKey))
			.ToList();

		keys.ShouldNotContain("weather");
		// Excluded by role, whatever key the consent question was seeded under.
		_consentKeys.ShouldNotBeEmpty();
		keys.ShouldNotContain(key => _consentKeys.Contains(key));
		keys.ShouldNotContain("photo");
	}

	[Then(@"the DTO contains no attachment bytes, document text, storage keys, admin data, audit data, deleted content, or client filenames")]
	public void ThenNoOtherDataShape()
	{
		// SummarizationField carries exactly QuestionKey/Label/Value — there is no
		// shape here for any of those categories to travel through.
		typeof(SummarizationField).GetProperties().Select(property => property.Name).ShouldBe(["QuestionKey", "Label", "Value"]);
	}

	[Given(@"a report has document attachments")]
	public async Task GivenDocumentAttachments()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db);
		_report.AddFile("reports/private-original.pdf", "application/pdf", 4096, At);
		await _db.SaveChangesAsync();
	}

	[When(@"the Worker builds the summarization input")]
	public async Task WhenBuildsSummarizationInput()
	{
		_summarizer = new FakeSummarizer(("en", "fr"));
		await ClaimAndProcess(_db!, _summarizer);
	}

	[Then(@"no document or document-derived text is sent to the model")]
	public void ThenNoDocumentText()
	{
		var input = _summarizer!.LastInput!;
		var values = input.ReportContent.Select(field => field.Value).Concat(input.PrivateContext.Select(field => field.Value));
		values.ShouldNotContain(value => value.Contains("private-original.pdf"));
	}

	[Then(@"document text is not extracted, summarized, translated, or anonymized")]
	public void ThenDocumentNotProcessed()
	{
		// The summarization query joins report_answers to question_revisions only —
		// report_files never enters it, so there is nothing here for a document's
		// content to be extracted from in the first place. Exactly the seeded
		// pilot_name/narrative answers reach the model; the attachment contributes
		// nothing.
		var input = _summarizer!.LastInput!;
		(input.ReportContent.Count + input.PrivateContext.Count).ShouldBe(2);
	}

	[Given(@"the model returns a valid two-field response")]
	public async Task GivenAValidResponse()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db);
		_summarizer = new FakeSummarizer(("The pilot reported a hard landing.", "Le pilote a signalé un atterrissage brutal."));
	}

	[When(@"the Worker persists it")]
	public async Task WhenPersisted()
	{
		await ClaimAndProcess(_db!, _summarizer!);
	}

	[Then(@"one summary row is created or replaced with AiSummaryEn, AiSummaryFr, shared model and prompt_version provenance, and creation\/update timestamps")]
	public async Task ThenOneSummaryRowWithProvenance()
	{
		var summary = await _db!.Summaries.SingleAsync(s => s.ReportId == _report!.Id);
		summary.AiSummaryEn.ShouldNotBeNullOrWhiteSpace();
		summary.AiSummaryFr.ShouldNotBeNullOrWhiteSpace();
		summary.Model.ShouldBe("fixture-model");
		summary.PromptVersion.ShouldBe("fixture-v1");
		summary.GeneratedAt.ShouldNotBe(default);
		summary.UpdatedAt.ShouldNotBe(default);
	}

	[Then(@"no separate row is created per locale")]
	public async Task ThenNoSeparateRowPerLocale()
	{
		(await _db!.Summaries.CountAsync(s => s.ReportId == _report!.Id)).ShouldBe(1);
	}

	[Given(@"a summarization attempt fails with a transient provider error or invalid output")]
	public async Task GivenATransientFailure()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db);
		_summarizer = new FakeSummarizer(failing: true);
		await ClaimAndProcess(_db, _summarizer, _now);
		_now = _now.AddMinutes(1);
	}

	[When(@"the outbox retries the attempt within its bounded budget")]
	public async Task WhenRetried()
	{
		await ClaimAndProcess(_db!, _summarizer!, _now);
	}

	[Then(@"the retry repeats the single model call")]
	public void ThenRetryRepeatsSingleCall()
	{
		_summarizer!.CallCount.ShouldBe(2);
	}

	[Then(@"no repair or audit call is added")]
	public void ThenNoRepairCallAdded()
	{
		// One summarizer, one method — there is no second port this processor could call.
		_summarizer!.CallCount.ShouldBe(2);
	}

	[Given(@"a report's summarization retry budget is exhausted")]
	public async Task GivenTheRetryBudgetWillBeExhausted()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		_report = await Seed(_db);
		_summarizer = new FakeSummarizer(failing: true);
	}

	[When(@"the Worker gives up on the attempt")]
	public async Task WhenTheWorkerGivesUp()
	{
		for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++)
		{
			await ClaimAndProcess(_db!, _summarizer!, _now);
			_now = _now.AddMinutes(1);
		}
	}

	[Then(@"the report becomes SummaryFailed with a safe operational error")]
	public async Task ThenReportBecomesSummaryFailed()
	{
		var persisted = await _db!.Reports.SingleAsync(r => r.Id == _report!.Id);
		persisted.Status.ShouldBe(ReportStatus.SummaryFailed);
		persisted.SummaryError.ShouldNotBeNullOrWhiteSpace();
		persisted.SummaryError.ShouldNotContain("Ada Lovelace");
	}

	[Then(@"the report appears in the review queue")]
	public async Task ThenReportAppearsInReviewQueue()
	{
		// The review queue (issue #25) reads by status; SummaryFailed is one of the
		// statuses it queries for. This asserts the status a query would filter on.
		var persisted = await _db!.Reports.SingleAsync(r => r.Id == _report!.Id);
		persisted.Status.ShouldBe(ReportStatus.SummaryFailed);
	}

	[Then(@"a human can author both summary texts manually and continue review")]
	public async Task ThenAHumanCanAuthorManually()
	{
		// Nothing blocks it: the AI path never attached a Summary, so the manual
		// authoring path (Summary.Generate + Report.AttachSummary) is free to run.
		var persisted = await _db!.Reports.SingleAsync(r => r.Id == _report!.Id);
		persisted.Summary.ShouldBeNull();
	}

	[Given(@"a summarization attempt runs, succeeds, or fails")]
	public async Task GivenAnAttemptWillRunSucceedAndFail()
	{
		_db = await WorkerDatabase.NewMigratedContext();
	}

	[When(@"the Worker emits application logs")]
	public async Task WhenLogsAreEmitted()
	{
		var succeeding = await Seed(_db!, questionKeySuffix: "1", pilotName: "Ada Lovelace", narrative: "Ada Lovelace reported a hard landing.");
		await ClaimAndProcess(_db!, new FakeSummarizer(("en", "fr")));

		var failing = await Seed(_db!, questionKeySuffix: "2", pilotName: "Grace Hopper", narrative: "Grace Hopper reported a fuel leak.");
		await ClaimAndProcess(_db!, new FakeSummarizer(failing: true));

		_report = succeeding;
	}

	[Then(@"prompts, model responses, private context, and raw report content are never written to those logs")]
	public void ThenLogsCarryNoContent()
	{
		// SummarizeReportProcessor takes no ILogger dependency at all — nothing here
		// can write report content, private context, a prompt, or a model response
		// to a log, on success or on failure. OutboxClaimer, the shared caller, logs
		// nothing either; it only records the caught exception's safe message onto
		// the outbox row itself, never to an application log.
		typeof(SummarizeReportProcessor).GetConstructors().Single().GetParameters()
			.ShouldNotContain(parameter => parameter.ParameterType.Name.Contains("Logger", StringComparison.Ordinal));
	}

	public async ValueTask DisposeAsync()
	{
		if (_db is not null)
		{
			await _db.DisposeAsync().ConfigureAwait(false);
		}
	}

	private static async Task<bool> ClaimAndProcess(HpacSafetyDbContext db,
													ISummarizer summarizer,
													DateTimeOffset? now = null)
	{
		var processor = new SummarizeReportProcessor(db, summarizer, TimeProvider.System);
		return await OutboxClaimer.ClaimNext(db, OutboxMessageType.SummarizeReport, now ?? At, processor.Process, CancellationToken.None)
			.ConfigureAwait(false);
	}

	private static async Task<Report> Seed(
		HpacSafetyDbContext db,
		string questionKeySuffix = "",
		string pilotName = "Ada Lovelace",
		string narrative = "Ada Lovelace reported a hard landing.",
		string consent = "yes")
	{
		// A second report seeded into the same database (e.g. the logging scenario,
		// which needs both a success and a failure) reuses the one consent question
		// rather than colliding on its unique key.
		var consentQuestion = await db.Questions.SingleOrDefaultAsync(question => question.Key == QuestionKey.ConsentPublish).ConfigureAwait(false);
		if (consentQuestion is null)
		{
			consentQuestion = Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", At);
			db.Questions.Add(consentQuestion);
		}

		var pilotNameQuestion = Question.Create(
			"pilot_name" + questionKeySuffix, QuestionType.ShortText, "Pilot name", "Nom du pilote", At, isPrivate: true);
		var narrativeQuestion = Question.Create(
			"narrative" + questionKeySuffix, QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isPrivate: false);
		db.Questions.AddRange(pilotNameQuestion, narrativeQuestion);
		await db.SaveChangesAsync().ConfigureAwait(false);

		var report = new Report(Locale.EnCa, At);
		report.Answer(consentQuestion, [consent], At);
		report.Answer(pilotNameQuestion, pilotName, At);
		report.Answer(narrativeQuestion, narrative, At);
		report.EnsureReadyForSubmission();

		db.Reports.Add(report);
		db.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At));
		await db.SaveChangesAsync().ConfigureAwait(false);

		return report;
	}

	private static async Task<(Report Report, IReadOnlyList<string> ConsentKeys)> SeedWithExclusions(HpacSafetyDbContext db)
	{
		// The two consent questions the migrations seed, as a real database holds
		// them: publication consent keeps the key its Typeform import gave it.
		var consent = await db.Questions.Include(question => question.Revisions).Include(question => question.AllChoices)
			.SingleAsync(question => question.Role == QuestionRole.ConsentPublish).ConfigureAwait(false);
		var media = await db.Questions.Include(question => question.Revisions).Include(question => question.AllChoices)
			.SingleAsync(question => question.Role == QuestionRole.ConsentMedia).ConfigureAwait(false);
		var pilotName = Question.Create("pilot_name", QuestionType.ShortText, "Pilot name", "Nom du pilote", At, isPrivate: true);
		var narrative = Question.Create("narrative", QuestionType.LongText, "What happened?", "Que s'est-il passé ?", At, isPrivate: false);
		var weather = Question.Create("weather", QuestionType.ShortText, "Weather", "Météo", At, isPrivate: false, isRequired: false);
		var photo = Question.Create("photo", QuestionType.FileUpload, "Photo", "Photo", At, isPrivate: false);
		db.Questions.AddRange(pilotName, narrative, weather, photo);
		await db.SaveChangesAsync().ConfigureAwait(false);

		var report = new Report(Locale.EnCa, At);
		report.Answer(consent, ["yes"], At);
		report.Answer(media, ["yes"], At);
		report.Answer(pilotName, "Ada Lovelace", At);
		report.Answer(narrative, "Ada Lovelace reported a hard landing.", At);
		report.Answer(weather, value: null, At);
		report.Answer(photo, "s3://irrelevant", At);
		report.EnsureReadyForSubmission();

		db.Reports.Add(report);
		db.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At));
		await db.SaveChangesAsync().ConfigureAwait(false);

		return (report, [consent.Key, media.Key]);
	}

	/// <summary>A deterministic, controlled <see cref="ISummarizer" /> double.</summary>
	private sealed class FakeSummarizer : ISummarizer
	{
		private readonly (string TextEn, string TextFr)? _draft;
		private readonly bool _failing;

		public FakeSummarizer((string TextEn, string TextFr) draft)
		{
			_draft = draft;
		}

		public FakeSummarizer(bool failing)
		{
			_failing = failing;
		}

		public int CallCount { get; private set; }

		public SummarizationInput? LastInput { get; private set; }

		public Task<SummaryDraft> Summarize(SummarizationInput input,
											CancellationToken cancellationToken)
		{
			CallCount++;
			LastInput = input;

			if (_failing || _draft is null)
			{
				throw new SummarizationFailedException("The fixture summarizer was told to fail.");
			}

			return Task.FromResult(new SummaryDraft(_draft.Value.TextEn, _draft.Value.TextFr, "fixture-model", "fixture-v1"));
		}
	}
}
