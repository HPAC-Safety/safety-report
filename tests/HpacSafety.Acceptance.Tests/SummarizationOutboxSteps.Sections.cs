using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Summarization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The summary-section scenarios (REQ-AI-031 to REQ-AI-036, ADR-0180): which
///     sections the Worker tells the model to write, in what order the answers
///     arrive, where a heading's words come from, and what the Worker does with a
///     summary whose headings differ.
/// </summary>
public sealed partial class SummarizationOutboxSteps
{
	private const string DescriptionKey = "description";
	private const string ActionKey = "action_and_prevention";
	private const string PrivateNarrativeKey = "private_narrative";

	private readonly Dictionary<string, Question> _sectionQuestions = new(StringComparer.Ordinal);
	private Exception? _headingFailure;
	private IReadOnlyList<SummarizationSection> _headingSections = [];
	private StubMediator? _mediator;
	private OpenAiSummarizer? _realSummarizer;

	/// <summary>The cases REQ-AI-034's table names, as the English and French summaries the model returned.</summary>
	private static readonly Dictionary<string, (string En, string Fr)> HeadingCases = new(StringComparer.Ordinal)
	{
		["both sections, in form order, in each language's own label"] =
			("## Description\nA landing.\n\n## Action and prevention\nThe club was told.",
			 "## Description\nUn atterrissage.\n\n## Action et prévention\nLe club a été informé."),
		["both sections, each followed by \"Not provided.\" or \"Non fourni.\""] =
			("## Description\nNot provided.\n\n## Action and prevention\nNot provided.",
			 "## Description\nNon fourni.\n\n## Action et prévention\nNon fourni."),
		["only the first section"] =
			("## Description\nA landing.", "## Description\nUn atterrissage."),
		["the two sections in the other order"] =
			("## Action and prevention\nThe club was told.\n\n## Description\nA landing.",
			 "## Action et prévention\nLe club a été informé.\n\n## Description\nUn atterrissage."),
		["both sections and an extra \"## Notes\" section"] =
			("## Description\nA landing.\n\n## Action and prevention\nTold.\n\n## Notes\nMore.",
			 "## Description\nUn atterrissage.\n\n## Action et prévention\nInformé.\n\n## Notes\nPlus."),
		["both sections, the first as a level-one \"# \" heading"] =
			("# Description\nA landing.\n\n## Action and prevention\nTold.",
			 "# Description\nUn atterrissage.\n\n## Action et prévention\nInformé."),
		["the first section reworded"] =
			("## What happened\nA landing.\n\n## Action and prevention\nTold.",
			 "## Ce qui s'est passé\nUn atterrissage.\n\n## Action et prévention\nInformé."),
		["the French summary headed with the English labels"] =
			("## Description\nA landing.\n\n## Action and prevention\nTold.",
			 "## Description\nUn atterrissage.\n\n## Action and prevention\nInformé."),
		["no headings at all"] =
			("A landing, then the club was told.", "Un atterrissage, puis le club a été informé."),
		["a \"## Description\" heading"] =
			("## Description\nA landing.", "## Description\nUn atterrissage."),
	};

	// --- REQ-AI-031: one section per public paragraph question, blank ones included ---

	[Given(@"a consented report answered two public paragraph questions and one private paragraph question")]
	public async Task GivenTwoPublicAndOnePrivateParagraphQuestions()
	{
		_db = await WorkerDatabase.NewMigratedContext();

		// Display order differs from key order, so a summary that followed either
		// the answer order or the alphabet would fail.
		await AddSectionQuestions(_db,
			(DescriptionKey, QuestionType.LongText, "Description", "Description", false, 10),
			(ActionKey, QuestionType.LongText, "Action and prevention", "Action et prévention", false, 20),
			(PrivateNarrativeKey, QuestionType.LongText, "Private narrative", "Récit privé", true, 30));
	}

	[Given(@"one of the public paragraph questions was left blank")]
	public async Task GivenOneParagraphWasLeftBlank()
	{
		_report = await SeedAnswers(_db!, [
			(_sectionQuestions[PrivateNarrativeKey], "A private synthetic narrative."),
			(_sectionQuestions[DescriptionKey], "A synthetic hard landing."),
			(_sectionQuestions[ActionKey], null),
		]);
	}

	[Then(@"the expected sections name both public paragraph questions, the blank one included")]
	public void ThenTheExpectedSectionsNameBothPublicQuestions()
	{
		_summarizer!.LastInput!.ExpectedSections.Select(section => section.QuestionKey).ShouldBe([DescriptionKey, ActionKey], ignoreOrder: true);
	}

	[Then(@"the private paragraph question has no expected section")]
	public void ThenThePrivateQuestionHasNoSection()
	{
		_summarizer!.LastInput!.ExpectedSections.ShouldNotContain(section => section.QuestionKey == PrivateNarrativeKey);
	}

	[Then(@"the expected sections are in the questions' display order")]
	public void ThenTheExpectedSectionsAreInDisplayOrder()
	{
		_summarizer!.LastInput!.ExpectedSections.Select(section => section.QuestionKey).ShouldBe([DescriptionKey, ActionKey]);
	}

	// --- REQ-AI-032: answers reach the model in form order ---

	[Given(@"a consented report whose answers were recorded in a different order than their questions' display order")]
	public async Task GivenAnswersRecordedOutOfFormOrder()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		await AddSectionQuestions(_db,
			("late_question", QuestionType.ShortText, "Late question", "Question tardive", false, 30),
			("first_question", QuestionType.ShortText, "First question", "Première question", false, 10),
			("middle_question", QuestionType.ShortText, "Middle question", "Question du milieu", false, 20));

		_report = await SeedAnswers(_db, [
			(_sectionQuestions["late_question"], "late"),
			(_sectionQuestions["first_question"], "first"),
			(_sectionQuestions["middle_question"], "middle"),
		]);
	}

	[Then(@"report_content lists the answers in display order")]
	public void ThenReportContentIsInDisplayOrder()
	{
		_summarizer!.LastInput!.ReportContent.Select(field => field.QuestionKey).ShouldBe(["first_question", "middle_question", "late_question"]);
	}

	// --- REQ-AI-033: a heading's words are the label the reporter answered ---

	[Given(@"a consented report answered a public paragraph question whose label has since been reworded")]
	public async Task GivenAReportAnsweredAQuestionSinceReworded()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		await AddSectionQuestions(_db, (DescriptionKey, QuestionType.LongText, "Description:", "Description :", false, 10));

		var question = _sectionQuestions[DescriptionKey];
		_report = await SeedAnswers(_db, [(question, "A synthetic hard landing.")]);

		// The wording changes after the answer was given; the answer keeps its revision.
		question.Revise(QuestionType.LongText, "What happened", "Que s'est-il passé", false, true, 10, At.AddDays(1));
		await _db.SaveChangesAsync();
	}

	[Given(@"the answered label was stored with a trailing colon")]
	public async Task GivenTheAnsweredLabelHadAColon()
	{
		var answer = await _db!.ReportAnswers.SingleAsync(candidate => candidate.ReportId == _report!.Id && candidate.QuestionKey == DescriptionKey);
		var answered = await _db.QuestionRevisions.SingleAsync(revision => revision.Id == answer.QuestionRevisionId);
		answered.LabelEn.ShouldEndWith(":");
		answered.LabelFr.ShouldEndWith(":");
	}

	[Then(@"the expected section carries the English and French labels of the revision the reporter answered")]
	public void ThenTheSectionCarriesTheAnsweredLabels()
	{
		var section = _summarizer!.LastInput!.ExpectedSections.ShouldHaveSingleItem();
		section.QuestionKey.ShouldBe(DescriptionKey);
		section.LabelEn.ShouldStartWith("Description");
		section.LabelFr.ShouldStartWith("Description");
		section.LabelEn.ShouldNotContain("What happened");
	}

	[Then(@"neither label ends in a colon")]
	public void ThenNeitherLabelEndsInAColon()
	{
		var section = _summarizer!.LastInput!.ExpectedSections.ShouldHaveSingleItem();
		section.LabelEn.ShouldBe("Description");
		section.LabelFr.ShouldBe("Description");
	}

	// --- REQ-AI-034, REQ-AI-035: the Worker checks the headings ---

	[Given(@"^a report whose expected sections are ""Description"" and ""Action and prevention"" \/ ""Action et prévention""$")]
	public void GivenAReportWithTwoExpectedSections()
	{
		_headingSections =
		[
			new SummarizationSection(DescriptionKey, "Description", "Description"),
			new SummarizationSection(ActionKey, "Action and prevention", "Action et prévention"),
		];
	}

	[Given(@"a report with no public paragraph question")]
	public void GivenAReportWithNoParagraphQuestion()
	{
		_headingSections = [];
	}

	[When(@"^the Worker validates a summary whose headings are (.+)$")]
	public async Task WhenTheWorkerValidatesHeadings(string headings)
	{
		HeadingCases.ShouldContainKey(headings);
		var (english, french) = HeadingCases[headings];
		_mediator = new StubMediator(english, french);

		try
		{
			await NewSummarizer(_mediator).Summarize(
				SummarizationInput.Partition(
					[new ClassifiedReportField(new SummarizationField(DescriptionKey, "Description", "A synthetic hard landing."), false)],
					_headingSections),
				CancellationToken.None);
			_headingFailure = null;
		}
		catch (SummarizationFailedException failure)
		{
			_headingFailure = failure;
		}
	}

	[Then(@"^the summary is (accepted|rejected)$")]
	public void ThenTheSummaryIs(string outcome)
	{
		if (outcome == "accepted")
		{
			_headingFailure.ShouldBeNull();
		}
		else
		{
			_headingFailure.ShouldBeOfType<SummarizationFailedException>();
		}
	}

	// --- REQ-AI-036: wrong headings are a failed attempt under the retry budget ---

	[Given(@"a consented report with a public paragraph question is due for summarization")]
	public async Task GivenAConsentedReportWithAParagraphQuestionIsDue()
	{
		_db = await WorkerDatabase.NewMigratedContext();
		await AddSectionQuestions(_db, (DescriptionKey, QuestionType.LongText, "Description", "Description", false, 10));
		_report = await SeedAnswers(_db, [(_sectionQuestions[DescriptionKey], "A synthetic hard landing.")]);
	}

	[Given(@"the model answers every attempt with headings that do not match the expected sections")]
	public void GivenTheModelAnswersWithTheWrongHeadings()
	{
		_mediator = new StubMediator("## Something else\nA landing.", "## Autre chose\nUn atterrissage.");
		_realSummarizer = NewSummarizer(_mediator);
	}

	[When(@"the outbox retries the attempt until its budget is exhausted")]
	public async Task WhenTheOutboxRetriesUntilTheBudgetIsExhausted()
	{
		for (var attempt = 0; attempt < OutboxMessage.PoisonThreshold; attempt++)
		{
			await ClaimAndProcess(_db!, _realSummarizer!, _now);
			_now = _now.AddMinutes(1);
		}
	}

	[Then(@"each attempt fails and none saves a summary")]
	public async Task ThenEachAttemptFailsAndNoneSaves()
	{
		_mediator!.CallCount.ShouldBe(OutboxMessage.PoisonThreshold);
		(await _db!.Summaries.AnyAsync(summary => summary.ReportId == _report!.Id)).ShouldBeFalse();
	}

	[Then(@"the report becomes Summary failed")]
	public async Task ThenTheReportBecomesSummaryFailed()
	{
		(await _db!.Reports.SingleAsync(report => report.Id == _report!.Id)).Status.ShouldBe(ReportStatus.SummaryFailed);
	}

	// --- helpers ---

	private static OpenAiSummarizer NewSummarizer(StubMediator mediator)
	{
		return new OpenAiSummarizer(
			mediator,
			Options.Create(new AiChatClientOptions { Model = "gemini-3.7-flash", ReasoningEffort = ReasoningEffort.Low }));
	}

	private async Task AddSectionQuestions(HpacSafetyDbContext db,
										   params (string Key, QuestionType Type, string LabelEn, string LabelFr, bool IsPrivate, int Order)[] questions)
	{
		foreach (var (key, type, labelEn, labelFr, isPrivate, order) in questions)
		{
			var question = Question.Create(key, type, labelEn, labelFr, At, isPrivate: isPrivate, isActive: true, displayOrder: order);
			_sectionQuestions[key] = question;
			db.Questions.Add(question);
		}

		await db.SaveChangesAsync().ConfigureAwait(false);
	}

	/// <summary>A consented report with one answer per pair, in the order given; a null value is a skipped question.</summary>
	private static async Task<Report> SeedAnswers(HpacSafetyDbContext db,
												  IReadOnlyList<(Question Question, string? Value)> answers)
	{
		var consent = await db.Questions.Include(question => question.Revisions)
			.SingleAsync(question => question.Role == QuestionRole.ConsentPublish).ConfigureAwait(false);

		var report = new Report(Locale.EnCa, At);
		report.Answer(consent, true, At);

		foreach (var (question, value) in answers)
		{
			report.Answer(question, value, At);
		}

		report.EnsureReadyForSubmission();
		db.Reports.Add(report);
		db.OutboxMessages.Add(new OutboxMessage(report.Id, OutboxMessageType.SummarizeReport, report.Id.Value, At));
		await db.SaveChangesAsync().ConfigureAwait(false);

		return report;
	}

	/// <summary>Answers every call with one fixed English/French pair and counts the calls.</summary>
	private sealed class StubMediator(string english,
									  string french) : IAiMediator
	{
		public int CallCount { get; private set; }

		public bool IsConfigured => true;

		public Task<string> Complete(AiChatRequest request,
									 CancellationToken cancellationToken)
		{
			CallCount++;
			return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(
				new Dictionary<string, string> { ["ai_summary_en"] = english, ["ai_summary_fr"] = french }));
		}
	}
}
