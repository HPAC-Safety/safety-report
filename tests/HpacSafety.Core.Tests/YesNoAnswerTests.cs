using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     The yes/no vocabulary (ADR-0127): each language's words, their fixed
///     counterparts, and readers that accept all four.
/// </summary>
public class YesNoAnswerTests
{
	[Theory]
	[InlineData("en-CA", "yes", "no")]
	[InlineData("fr-CA", "oui", "non")]
	public void GivenLocale_WhenWordsAreRead_ThenTheyAreThatLanguages(string language,
																	  string yes,
																	  string no)
	{
		// Given
		var locale = Locale.Parse(language);

		// When / Then
		YesNoAnswer.Yes(locale).ShouldBe(yes);
		YesNoAnswer.No(locale).ShouldBe(no);
		YesNoAnswer.IsWordIn(yes, locale).ShouldBeTrue();
		YesNoAnswer.IsWordIn(no, locale).ShouldBeTrue();
		YesNoAnswer.IsWordIn(YesNoAnswer.Yes(locale.Counterpart), locale).ShouldBeFalse();
	}

	[Theory]
	[InlineData("yes", true, false, "oui")]
	[InlineData("oui", true, false, "yes")]
	[InlineData("no", false, true, "non")]
	[InlineData("non", false, true, "no")]
	[InlineData("Yes", false, false, null)]
	[InlineData("", false, false, null)]
	public void GivenStoredValue_WhenRead_ThenAllFourWordsAreUnderstood(string value,
																	   bool isYes,
																	   bool isNo,
																	   string? counterpart)
	{
		// Given / When / Then
		YesNoAnswer.IsYes(value).ShouldBe(isYes);
		YesNoAnswer.IsNo(value).ShouldBe(isNo);
		YesNoAnswer.Counterpart(value).ShouldBe(counterpart);
	}

	[Fact]
	public void GivenNoValue_WhenRead_ThenNeitherYesNorNo()
	{
		// Given / When / Then
		YesNoAnswer.IsYes(null).ShouldBeFalse();
		YesNoAnswer.IsNo(null).ShouldBeFalse();
		YesNoAnswer.Counterpart(null).ShouldBeNull();
	}
}
