using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     REQ-QB-120 and REQ-QB-121: a yes/no answer is a boolean, so it means the same
///     whatever language the report is written in, and only <c>true</c> enables a
///     conditional question or gives consent (ADR-0130). Both run against the
///     domain, where the condition and the consent projection live; every question
///     here is synthetic.
/// </summary>
[Binding]
public sealed class YesNoLanguageSteps
{
	private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	private Question? _parent;
	private Question? _child;
	private bool? _parentAnswer;
	private Report? _report;

	// --- REQ-QB-120 ---

	[Given(@"a question depends on a yes\/no question")]
	public void GivenAQuestionDependsOnAYesNoQuestion()
	{
		_parent = Question.Create("was_injured", QuestionType.YesNo, "Was anyone injured?", "Y a-t-il eu des blessés ?", Noon, isActive: true);
		_child = Question.Create(
			"injury_detail", QuestionType.ShortText, "Describe the injury", "Décrivez la blessure", Noon, isActive: true,
			dependsOnQuestionId: _parent.Id);
	}

	[When(@"^a reporter writing in (English|French) answers the yes\/no question (true|false)$")]
	public void WhenAReporterAnswersTheParent(string language,
											  bool answer)
	{
		// Recorded as the submission would record it, in the report's language.
		_parentAnswer = new Report(LocaleOf(language), Noon).Answer(_parent!, answer, Noon).BooleanValue;
	}

	[Then(@"^the conditional question is (asked|not asked)$")]
	public void ThenTheConditionalQuestionIs(string asked)
	{
		_child!.CurrentRevision.IsEnabledGiven(_parent, _parentAnswer).ShouldBe(asked == "asked");
	}

	// --- REQ-QB-121 ---

	[Given(@"^a reporter writing in (English|French) answers (consent_publish|consent_media) (true|false)$")]
	public void GivenAConsentAnswer(string language,
									string consent,
									bool answer)
	{
		_report = new Report(LocaleOf(language), Noon);
		_parentAnswer = answer;
		_parent = consent == QuestionKey.ConsentMedia
			? Question.CreateConsentMedia("May we show your files?", "Pouvons-nous montrer vos fichiers ?", Noon)
			: Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Noon);
	}

	[When(@"the answer is projected onto the report")]
	public void WhenTheAnswerIsProjected()
	{
		_report!.Answer(_parent!, _parentAnswer!.Value, Noon);
	}

	[Then(@"^the report records (consent_publish|consent_media) as (given|refused)$")]
	public void ThenTheReportRecordsConsent(string consent,
											string recorded)
	{
		var projected = consent == QuestionKey.ConsentMedia ? _report!.ConsentMedia : _report!.ConsentPublish;
		projected.ShouldBe(recorded == "given");
	}

	private static Locale LocaleOf(string language)
	{
		return language == "French" ? Locale.FrCa : Locale.EnCa;
	}
}
