using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using HpacSafety.Worker.Summarization;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Worker.Tests;

public sealed class PromptDrivenSummarizerTests
{
	private static SummarizationInput SampleInput()
	{
		return SummarizationInput.Partition([
			new ClassifiedReportField(new SummarizationField("pilot_name", "Pilot name", "Ada Lovelace"), true),
			new ClassifiedReportField(new SummarizationField("description", "Description", "Ada Lovelace reported a hard landing."), false)
		]);
	}

	private static PromptDrivenSummarizer BuildSummarizer(FixtureAiChatClient client, string model = "fixture-model")
	{
		return new PromptDrivenSummarizer(client, Options.Create(new AiChatClientOptions { Model = model }));
	}

	[Fact]
	public async Task GivenAValidTwoFieldResponse_WhenSummarized_ThenBothTextsAreReturnedWithProvenance()
	{
		// Given
		var client = new FixtureAiChatClient("""{"ai_summary_en":"The pilot reported a hard landing.","ai_summary_fr":"Le pilote a signalé un atterrissage brutal."}""");
		var summarizer = BuildSummarizer(client);

		// When
		var draft = await summarizer.SummarizeAsync(SampleInput(), CancellationToken.None);

		// Then
		draft.TextEn.ShouldBe("The pilot reported a hard landing.");
		draft.TextFr.ShouldBe("Le pilote a signalé un atterrissage brutal.");
		draft.Model.ShouldBe("fixture-model");
		draft.PromptVersion.ShouldBe(PromptDrivenSummarizer.CurrentPromptVersion);
	}

	[Fact]
	public async Task GivenASummarizationAttempt_WhenTheProviderIsCalled_ThenExactlyOneCallIsMade()
	{
		// Given
		var client = new FixtureAiChatClient("""{"ai_summary_en":"en","ai_summary_fr":"fr"}""");
		var summarizer = BuildSummarizer(client);

		// When
		await summarizer.SummarizeAsync(SampleInput(), CancellationToken.None);

		// Then
		client.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task GivenReportContentWithAMatchingPrivateValue_WhenTheProviderIsCalled_ThenTheUserMessageContainsTheMarker()
	{
		// Given
		var client = new FixtureAiChatClient("""{"ai_summary_en":"en","ai_summary_fr":"fr"}""");
		var summarizer = BuildSummarizer(client);

		// When
		await summarizer.SummarizeAsync(SampleInput(), CancellationToken.None);

		// Then — report_content is marked; private_context still carries the raw value
		// as recognition context (ADR-0082), so only the report_content field is checked.
		var messages = client.LastMessages.ShouldNotBeNull();
		var userMessage = messages[^1].Content;
		userMessage.ShouldContain("""value":"[PRIVATE:pilot_name] reported a hard landing.""");
	}

	[Theory]
	[InlineData("```json\n{\"ai_summary_en\":\"en\",\"ai_summary_fr\":\"fr\"}\n```")]
	[InlineData("""{"ai_summary_en":"en","ai_summary_fr":"fr","extra":"nope"}""")]
	[InlineData("""{"ai_summary_en":"","ai_summary_fr":"fr"}""")]
	[InlineData("""{"ai_summary_en":null,"ai_summary_fr":"fr"}""")]
	[InlineData("""{"ai_summary_en":"en"}""")]
	[InlineData("not json at all")]
	public async Task GivenAnInvalidResponse_WhenSummarized_ThenRejected(string response)
	{
		// Given
		var client = new FixtureAiChatClient(response);
		var summarizer = BuildSummarizer(client);

		// When
		var act = async () => await summarizer.SummarizeAsync(SampleInput(), CancellationToken.None);

		// Then
		await act.ShouldThrowAsync<SummarizationFailedException>();
	}

	[Fact]
	public async Task GivenAnUnconfiguredProvider_WhenSummarized_ThenFailsClosedWithoutLeakingReportContent()
	{
		// Given
		var client = new FixtureAiChatClient(response: "unused", isConfigured: false);
		var summarizer = BuildSummarizer(client);

		// When
		var act = async () => await summarizer.SummarizeAsync(SampleInput(), CancellationToken.None);

		// Then
		var exception = await act.ShouldThrowAsync<SummarizationFailedException>();
		exception.Message.ShouldNotContain("Ada Lovelace");
		exception.Message.ShouldNotContain("hard landing");
	}
}
