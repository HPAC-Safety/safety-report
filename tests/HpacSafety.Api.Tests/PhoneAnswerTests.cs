using HpacSafety.Api.Reports;
using Shouldly;

namespace HpacSafety.Api.Tests;

/// <summary>
///     A phone answer is an E.164 number valid for its country (ADR-0137). Every
///     number here is synthetic.
/// </summary>
public class PhoneAnswerTests
{
	[Theory]
	[InlineData("+16045551234", true)]
	[InlineData("+442079460018", true)]
	[InlineData("+33612345678", true)]
	[InlineData("+15555551234", false)]
	[InlineData("+1604555123", false)]
	[InlineData("+10", false)]
	[InlineData("+999123456", false)]
	[InlineData("6045551234", false)]
	public void GivenNumber_WhenChecked_ThenOnlyValidE164NumberPasses(string value,
																	  bool expected)
	{
		// Given / When / Then
		PhoneAnswer.IsValid(value).ShouldBe(expected);
	}
}
