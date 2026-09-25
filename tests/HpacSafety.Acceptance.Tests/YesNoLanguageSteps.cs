using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     REQ-QB-120 and REQ-QB-121: a yes or no means the same whichever language it
///     was stored in (ADR-0127). Both run against the domain, where the condition
///     and the consent projection live; every question here is synthetic.
/// </summary>
[Binding]
public sealed class YesNoLanguageSteps
{
	private static readonly DateTimeOffset Noon = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

	private Question? _parent;
	private Question? _child;
	private string? _parentAnswer;
	private Locale _language = Locale.EnCa;
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

	[When(@"^a reporter writing in (English|French) answers the yes\/no question (\w+)$")]
	public void WhenAReporterAnswersTheParent(string language,
											  string answer)
	{
		_language = language == "French" ? Locale.FrCa : Locale.EnCa;

		// Recorded as the submission would record it, so the value is one the
		// report's language accepts.
		_parentAnswer = new Report(_language, Noon).Answer(_parent!, answer, Noon).Value;
	}

	[Then(@"^the conditional question is (asked|not asked)$")]
	public void ThenTheConditionalQuestionIs(string asked)
	{
		_child!.CurrentRevision.IsEnabledGiven(_parent, _parentAnswer, _language).ShouldBe(asked == "asked");
	}

	// --- REQ-QB-121 ---

	[Given(@"^a reporter's answer to (consent_publish|consent_media) is stored as (\w+)$")]
	public void GivenAConsentAnswerIsStored(string consent,
											string stored)
	{
		// The word decides the report's language: a French report stores oui/non.
		_language = stored is "oui" or "non" ? Locale.FrCa : Locale.EnCa;
		_report = new Report(_language, Noon);
		_parentAnswer = stored;
		_parent = consent == QuestionKey.ConsentMedia
			? Question.CreateConsentMedia("May we show your files?", "Pouvons-nous montrer vos fichiers ?", Noon)
			: Question.CreateConsentPublish("May we publish?", "Pouvons-nous publier ?", Noon);
	}

	[When(@"the answer is projected onto the report")]
	public void WhenTheAnswerIsProjected()
	{
		_report!.Answer(_parent!, _parentAnswer, Noon);
	}

	[Then(@"^the report records (consent_publish|consent_media) as (given|refused)$")]
	public void ThenTheReportRecordsConsent(string consent,
											string recorded)
	{
		var projected = consent == QuestionKey.ConsentMedia ? _report!.ConsentMedia : _report!.ConsentPublish;
		projected.ShouldBe(recorded == "given");
	}
}
