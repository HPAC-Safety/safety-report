using HpacSafety.Core.Features.Reporting;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The deterministic marking-pass scenarios in
///     <c>features/ai-anonymization/ai-anonymization.feature</c> — ADR-0081. These
///     assert <see cref="PrivateValueMarker" /> directly, the same domain rule an end
///     to end summarization attempt (issue #17) applies before its one model call.
/// </summary>
[Binding]
public sealed class AiAnonymizationSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string PrivateQuestionKey = "pilot_name";
	private const string ReportContentQuestionKey = "description";

	private SummarizationInput _input = null!;
	private SummarizationInput _marked = null!;

	[Given(@"a private answer's value appears verbatim in a report_content field")]
	public void GivenAPrivateValueAppearsVerbatimInReportContent()
	{
		BuildInput(privateValue: "Ada Lovelace", reportContentValue: "Ada Lovelace reported a hard landing.");
	}

	[Given(@"a private answer's value is multiple words")]
	public void GivenAPrivateValueIsMultipleWords()
	{
		// Asserted by the fixture the next step builds — "Ada Lovelace" is two words.
	}

	[Given(@"one of its words, at or above the minimum match length and not on the stopword list, appears alone in a report_content field")]
	public void GivenOneOfItsWordsAppearsAloneInReportContent()
	{
		"Lovelace".Length.ShouldBeGreaterThanOrEqualTo(PrivateValueMarker.MinimumTokenLength);
		BuildInput(privateValue: "Ada Lovelace", reportContentValue: "Lovelace radioed the tower.");
	}

	[Given(@"a report_content field contains a word that is below the minimum match length or on the stopword list")]
	public void GivenAReportContentFieldContainsAShortOrStopword()
	{
		BuildInput(privateValue: "North Bay Airport", reportContentValue: "The aircraft flew north before landing.");
	}

	[Given(@"that word also appears as a token of a private answer's value")]
	public void GivenThatWordAlsoAppearsAsAPrivateToken()
	{
		// Already true of the fixture built above — "north" is a token of "North Bay Airport".
	}

	[Given(@"a report_content field contains a private answer's whole multi-word value verbatim")]
	public void GivenAReportContentFieldContainsTheWholeValueVerbatim()
	{
		BuildInput(privateValue: "Ada Lovelace", reportContentValue: "Ada Lovelace reported the failure.");
	}

	[Given(@"a private answer's value appears in a report_content field with different casing or extra whitespace")]
	public void GivenAPrivateValueAppearsWithDifferentCasingOrWhitespace()
	{
		BuildInput(privateValue: "Ada Lovelace", reportContentValue: "ADA   LOVELACE was flying.");
	}

	[Given(@"the Worker has built the marked report_content for a report")]
	public void GivenTheWorkerHasBuiltMarkedReportContent()
	{
		BuildInput(privateValue: "Ada Lovelace", reportContentValue: "Ada Lovelace reported a hard landing.");
		_marked = PrivateValueMarker.Mark(_input);
	}

	[When(@"the Worker builds the marked report_content")]
	public void WhenTheWorkerBuildsTheMarkedReportContent()
	{
		_marked = PrivateValueMarker.Mark(_input);
	}

	[When(@"the Worker builds the model input DTO")]
	public void WhenTheWorkerBuildsTheModelInputDto()
	{
		_marked = PrivateValueMarker.Mark(_input);
	}

	[Then(@"that occurrence is replaced with a marker naming the private question it came from")]
	public void ThenThatOccurrenceIsReplacedWithAMarker()
	{
		_marked.ReportContent.Single().Value.ShouldContain($"[PRIVATE:{PrivateQuestionKey}]");
	}

	[Then(@"the replacement happens before the model call, not as a separate call or stage")]
	public void ThenTheReplacementHappensBeforeTheModelCall()
	{
		// PrivateValueMarker.Mark is a pure, synchronous transform over SummarizationInput —
		// it has no provider dependency and makes no call of its own.
		typeof(PrivateValueMarker).GetMethod(nameof(PrivateValueMarker.Mark))!
			.ReturnType.ShouldBe(typeof(SummarizationInput));
	}

	[Then(@"that word is left unmarked")]
	public void ThenThatWordIsLeftUnmarked()
	{
		_marked.ReportContent.Single().Value.ShouldBe("The aircraft flew north before landing.");
	}

	[Then(@"the whole value is replaced with a single marker")]
	public void ThenTheWholeValueIsReplacedWithASingleMarker()
	{
		_marked.ReportContent.Single().Value.ShouldBe($"[PRIVATE:{PrivateQuestionKey}] reported the failure.");
	}

	[Then(@"its individual words are not separately marked inside that same span")]
	public void ThenItsIndividualWordsAreNotSeparatelyMarked()
	{
		_marked.ReportContent.Single().Value.ShouldNotContain(
			$"[PRIVATE:{PrivateQuestionKey}] [PRIVATE:{PrivateQuestionKey}]");
	}

	[Then(@"that occurrence is still replaced with a marker")]
	public void ThenThatOccurrenceIsStillReplacedWithAMarker()
	{
		_marked.ReportContent.Single().Value.ShouldBe($"[PRIVATE:{PrivateQuestionKey}] was flying.");
	}

	[Then(@"private_context still contains every private answered field, unchanged")]
	public void ThenPrivateContextStillContainsEveryPrivateAnsweredField()
	{
		_marked.PrivateContext.ShouldBe(_input.PrivateContext);
	}

	[Then(@"the model receives both the marked report_content and the unmarked private_context")]
	public void ThenTheModelReceivesBothSections()
	{
		_marked.ReportContent.Single().Value.ShouldContain($"[PRIVATE:{PrivateQuestionKey}]");
		_marked.PrivateContext.Single().Value.ShouldBe("Ada Lovelace");
	}

	private void BuildInput(string privateValue, string reportContentValue)
	{
		_input = SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField(PrivateQuestionKey, "Pilot name", privateValue), true),
			new ClassifiedReportField(new SummarizationField(ReportContentQuestionKey, "Description", reportContentValue), false)
		]);
	}
}
