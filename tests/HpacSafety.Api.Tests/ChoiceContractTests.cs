using HpacSafety.Api.Admin;
using HpacSafety.Api.PublicQuestions;
using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     How a question's own choices cross the API boundary: the code an authored
///     choice is recorded under, and how a one-language choice reaches the public
///     form (ADR-0095).
/// </summary>
public class ChoiceContractTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData("mount_7", "Mount Seven", "Mont Sept", "mount_7")]
	[InlineData(null, "Mount 7", "Mont 7", "mount_7")]
	[InlineData(" ", "", "Élévation Sainte-Anne", "elevation_sainte_anne")]
	public void GivenChoiceInput_WhenCodeIsResolved_ThenExistingCodeThenEnglishThenFrenchDecides(
		string? code,
		string labelEn,
		string labelFr,
		string expected)
	{
		new OptionInput(code, labelEn, labelFr).ResolvedCode.ShouldBe(expected);
	}

	[Fact]
	public void GivenChoiceWithNoCodeAndNoWording_WhenCodeIsResolved_ThenRefused()
	{
		Should.Throw<DomainRuleViolationException>(() => new OptionInput(null, " ", null).ResolvedCode);
	}

	[Theory]
	[InlineData("en-CA", "Mount 7", "en-CA")]
	[InlineData("fr-CA", "Élévation", "fr-CA")]
	public void GivenOneLanguageReporterChoice_WhenOfferedPublicly_ThenBothLabelsCarryItAndItsLanguageIsNamed(
		string locale,
		string typed,
		string onlyIn)
	{
		// Given
		var question = Question.Create("site", QuestionType.Autocomplete, "Site", "Site", At, isActive: true);
		var choice = question.AddChoiceFromReporter(typed, Locale.Parse(locale));

		// When
		var view = PublicOptionView.Of(choice);

		// Then
		view.LabelEn.ShouldBe(typed);
		view.LabelFr.ShouldBe(typed);
		view.OnlyIn.ShouldBe(onlyIn);
	}

	[Fact]
	public void GivenBilingualChoice_WhenOfferedPublicly_ThenNoLanguageIsSingledOut()
	{
		var question = Question.Create(
			"site", QuestionType.SingleSelect, "Site", "Site", At,
			options: [new QuestionOptionInput("coopers", "Cooper's Hill", "Colline Cooper")]);

		PublicOptionView.Of(question.Choice("coopers")!).OnlyIn.ShouldBeNull();
	}

	[Fact]
	public void GivenNoMergedValue_WhenOfferedPublicly_ThenItCarriesNoAliases()
	{
		var question = Question.Create("site", QuestionType.Autocomplete, "Site", "Site", At, isActive: true);
		var choice = question.AddChoiceFromReporter("Cooper's", Locale.EnCa);

		PublicOptionView.Of(choice, question.AliasesOf(choice.Id)).Aliases.ShouldBeEmpty();
	}

	[Fact]
	public void GivenAMergedValue_WhenTheSurvivorIsOfferedPublicly_ThenItCarriesTheMergedWordingAsAnAlias()
	{
		// Given
		var question = Question.Create("site", QuestionType.Autocomplete, "Site", "Site", At, isActive: true);
		var source = question.AddChoiceFromReporter("Coopers", Locale.EnCa);
		var target = question.AddChoiceFromReporter("Cooper's", Locale.EnCa);
		question.MergeValue(source.Id, target.Id, "synthetic-safety-officer", At);

		// When
		var view = PublicOptionView.Of(target, question.AliasesOf(target.Id));

		// Then
		view.Aliases.ShouldHaveSingleItem();
		view.Aliases[0].LabelEn.ShouldBe("Coopers");
	}

	[Fact]
	public void GivenAChainedMerge_WhenTheFinalSurvivorIsOfferedPublicly_ThenItCarriesEveryEarlierWordingAsAnAlias()
	{
		// Given
		var question = Question.Create("site", QuestionType.Autocomplete, "Site", "Site", At, isActive: true);
		var a = question.AddChoiceFromReporter("A", Locale.EnCa);
		var b = question.AddChoiceFromReporter("B", Locale.EnCa);
		var c = question.AddChoiceFromReporter("C", Locale.EnCa);
		question.MergeValue(a.Id, b.Id, "synthetic-safety-officer", At);
		question.MergeValue(b.Id, c.Id, "synthetic-safety-officer", At);

		// When
		var view = PublicOptionView.Of(c, question.AliasesOf(c.Id));

		// Then
		view.Aliases.Select(alias => alias.LabelEn).ShouldBe(["A", "B"], ignoreOrder: true);
	}
}
