using HpacSafety.Core.Features.QuestionBank;
using Shouldly;

namespace HpacSafety.Core.Tests;

/// <summary>A label has no closing colon; the interface draws it (ADR-0181, REQ-QB-244, REQ-TF-024).</summary>
public class QuestionLabelTests
{
	[Theory]
	[InlineData("Description:", "Description")]
	[InlineData("Description :", "Description")]
	[InlineData("Description :", "Description")]
	[InlineData("Description :", "Description")]
	[InlineData("Description : ", "Description")]
	[InlineData("Blessure (pilote):", "Blessure (pilote)")]
	[InlineData("Description", "Description")]
	[InlineData("Injured?", "Injured?")]
	[InlineData("Ratio 3:2 guide", "Ratio 3:2 guide")]
	public void GivenLabel_WhenTrailingColonRemoved_ThenOnlyAClosingColonGoes(string label,
																			   string expected)
	{
		// Given / When
		var trimmed = QuestionLabel.WithoutTrailingColon(label);

		// Then
		trimmed.ShouldBe(expected);
	}

	[Theory]
	[InlineData("Date:", true)]
	[InlineData("Date :", true)]
	[InlineData("Date", false)]
	[InlineData("Date?", false)]
	[InlineData("Ratio 3:2", false)]
	public void GivenLabel_WhenCheckedForColon_ThenOnlyAClosingColonCounts(string label,
																		   bool expected)
	{
		// Given / When / Then
		QuestionLabel.EndsWithColon(label).ShouldBe(expected);
	}
}
