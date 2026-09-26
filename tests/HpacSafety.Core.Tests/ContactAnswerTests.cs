using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>
///     The written forms of an email and a phone answer (ADR-0137). Every address
///     and number here is synthetic.
/// </summary>
public class ContactAnswerTests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData("pilot@example.com", true)]
	[InlineData("pilote.volant+club@exemple.qc.ca", true)]
	[InlineData("pilot@example.c", false)]
	[InlineData("pilot@.example.com", false)]
	[InlineData("pilot@example..com", false)]
	[InlineData("pilot@@example.com", false)]
	[InlineData("pilot@example.com\n", false)]
	[InlineData("", false)]
	public void GivenAddress_WhenChecked_ThenOnlyOneWellFormedAddressPasses(string value,
																		   bool expected)
	{
		// Given / When / Then
		ContactAnswer.IsEmailAddress(value).ShouldBe(expected);
	}

	[Fact]
	public void GivenAddressLongerThan254Characters_WhenChecked_ThenRefused()
	{
		// Given
		var value = new string('a', 64) + "@" + string.Join('.', Enumerable.Repeat(new string('b', 60), 3)) + ".example";

		// When / Then
		value.Length.ShouldBeGreaterThan(ContactAnswer.EmailMaxLength);
		ContactAnswer.IsEmailAddress(value).ShouldBeFalse();
	}

	[Theory]
	[InlineData("+16045551234", true)]
	[InlineData("+442079460018", true)]
	[InlineData("+06045551234", false)]
	[InlineData("16045551234", false)]
	[InlineData("+1 604 555 1234", false)]
	[InlineData("+1234567890123456", false)]
	[InlineData("+16045551234\n", false)]
	public void GivenNumber_WhenChecked_ThenOnlyE164Passes(string value,
														 bool expected)
	{
		// Given / When / Then
		ContactAnswer.IsE164(value).ShouldBe(expected);
	}

	[Theory]
	[InlineData(QuestionType.Email, "pilot@example")]
	[InlineData(QuestionType.Phone, "604-555-1234")]
	public void GivenMalformedAnswer_WhenRecorded_ThenRefusalNamesQuestionNotValue(QuestionType type,
																				   string value)
	{
		// Given
		var question = Question.Create("contact", type, "Contact", "Contact", Now, isActive: true);
		var report = new Report(Locale.EnCa, Now);

		// When
		var recording = () => report.Answer(question, value, Now);

		// Then
		var refusal = recording.ShouldThrow<DomainRuleViolationException>();
		refusal.Message.ShouldContain("'contact'");
		refusal.Message.ShouldNotContain(value);
		report.Answers.ShouldBeEmpty();
	}
}
