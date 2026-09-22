using HpacSafety.Core.Features.Reporting;
using Shouldly;

namespace HpacSafety.Anonymization.Tests;

public sealed class SummarizationFailedExceptionTests
{
	[Fact]
	public void GivenNoMessage_WhenCreated_ThenStillSaysSomethingUsable()
	{
		// Given / When
		var cause = new SummarizationFailedException();

		// Then
		cause.Message.ShouldBe("Summarization failed.");
	}
}
