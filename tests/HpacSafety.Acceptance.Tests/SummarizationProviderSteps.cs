using System.Net;
using System.Text;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using HpacSafety.Worker.Summarization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Reqnroll;
using Shouldly;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The model-provider scenarios in <c>.spec/features/ai-anonymization/ai-anonymization.feature</c>:
///     strict response validation (REQ-AI-011), the configured model and reasoning level on
///     the wire (REQ-AI-022), fail-closed startup (REQ-AI-023), and the rules the shipped
///     prompt carries (REQ-AI-024). Nothing here reaches a real provider (ADR-0104).
/// </summary>
[Binding]
public sealed class SummarizationProviderSteps
{
#pragma warning disable CA1822 // Reqnroll step bindings must be instance methods to be discovered.

	private const string ValidResponse = """{"ai_summary_en":"The pilot landed **safely**.","ai_summary_fr":"Le pilote s'est posé *sans incident*."}""";

	/// <summary>Each REQ-AI-024 example row, and the words the prompt must use to state it.</summary>
	private static readonly Dictionary<string, string[]> PromptRules = new(StringComparer.Ordinal)
	{
		["every statement must be supported by the report content, and nothing is invented"] =
			["must be supported by `report_content`", "Do not invent, assume, or infer"],
		["a pilot becomes exactly \"the pilot\" / \"le pilote\""] =
			["| the pilot | le pilote |"],
		["any other person becomes the role the report supports, or \"a person\" / \"une personne\""] =
			["Use the most accurate role the report supports", "| a person | une personne |"],
		["a place becomes a generic phrase such as \"the launch site\" or \"the location\" / \"le lieu\""] =
			["| the launch site | le site de décollage |", "| the location | le lieu |"],
		["an exact date becomes its month or season, and the time of day is kept"] =
			["An exact calendar date | its month and year, or its season", "A time of day | keep it exactly as reported"],
		["a club, school, or company becomes \"the club\", \"the school\", or \"the company\""] =
			["| the club, the school, the company |"],
		["an aircraft make or model becomes its category"] =
			["make or model | its category"],
		["\"redacted\", \"caviardé\", placeholders, and invented names are never written"] =
			["\"redacted\"", "\"caviardé\"", "a placeholder", "an invented name"],
		["every private marker is resolved and never appears literally"] =
			["Resolve every marker", "must never appear in a summary"],
		["the response is exactly the two-key English and French summary object"] =
			["{\"ai_summary_en\":\"...\",\"ai_summary_fr\":\"...\"}", "no additional key"],
		["each expected section is a \"## \" heading with its exact label, in form order"] =
			["`## ` followed by the exact `label_en`", "exact `label_fr`", "Write no other heading of any kind", "in the order given"],
		["other public facts are woven into the section they fit"] =
			["Weave the other facts of `report_content`", "no separate section for facts"],
		["each statement goes in the section whose question it best answers"] =
			["Put each statement in the section whose question it best answers", "even when the reporter typed it in a different answer"],
		["a section with nothing to say reads \"Not provided.\" / \"Non fourni.\""] =
			["exactly `Not provided.`", "exactly `Non fourni.`", "A section that received content from another answer is not empty"],
		["Markdown headings, bold, italic, and lists are allowed, and no other Markdown"] =
			["bold, italic, and lists or line breaks", "no other headings, links, images, tables, code, or raw HTML"],
	};

	private readonly Dictionary<string, string?> _settings = new()
	{
		["AiChatClient:Model"] = "gemini-3.7-flash",
		["AiChatClient:ReasoningEffort"] = "low",
	};

	private readonly List<(string Response, Exception? Failure)> _validations = [];

	private string? _requestBody;
	private Uri? _requestUri;
	private Exception? _startupFailure;
	private bool _claimLoopStarted;
	private string _prompt = string.Empty;

	// ── REQ-AI-011 ──────────────────────────────────────────────────────────

	[Given(@"the model returns a response for a summarization attempt")]
	public void GivenTheModelReturnsAResponse()
	{
		_settings["AiChatClient:ApiKey"] = "synthetic-test-key";
	}

	[When(@"the Worker validates the response")]
	public async Task WhenTheWorkerValidatesTheResponse()
	{
		string[] responses =
		[
			ValidResponse,
			"```json\n" + ValidResponse + "\n```",
			"Here is the summary: " + ValidResponse,
			"""{"ai_summary_en":"en","ai_summary_fr":"fr","ai_summary_notes":"extra"}""",
			"""{"ai_summary_en":null,"ai_summary_fr":"fr"}""",
			"""{"ai_summary_en":"en"}""",
		];

		foreach (var response in responses)
		{
			_validations.Add((response, await Attempt(response)));
		}
	}

	[Then(@"a response with exactly two nonblank string keys ""ai_summary_en"" and ""ai_summary_fr"", each a summary written as Markdown, is accepted")]
	public void ThenTheExactResponseIsAccepted()
	{
		_validations[0].Failure.ShouldBeNull();
	}

	[Then(@"a response with a Markdown fence around it, commentary, an extra key, a null value, or only one language is rejected")]
	public void ThenEveryOtherShapeIsRejected()
	{
		foreach (var (response, failure) in _validations.Skip(1))
		{
			failure.ShouldBeOfType<SummarizationFailedException>(response);
		}
	}

	// ── REQ-AI-022 ──────────────────────────────────────────────────────────

	[Given(@"the Worker is configured with a model and a reasoning level")]
	public void GivenTheWorkerIsConfigured()
	{
		_settings["AiChatClient:ApiKey"] = "synthetic-test-key";
		_settings["AiChatClient:ReasoningEffort"] = "medium";
	}

	[When(@"the Worker makes the summarization call")]
	public async Task WhenTheWorkerMakesTheCall()
	{
		(await Attempt(ValidResponse)).ShouldBeNull();
	}

	[Given(@"the model's name starts with ""gemini-""")]
	public void GivenTheModelNameStartsWithGemini()
	{
		_settings["AiChatClient:Model"].ShouldNotBeNull().ShouldStartWith("gemini-");
	}

	[Then(@"the call goes to Gemini's OpenAI-compatible service")]
	public void ThenTheCallGoesToGemini()
	{
		_requestUri.ShouldNotBeNull().ToString()
			.ShouldBe("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions");
	}

	[Then(@"no setting names a provider")]
	public void ThenNoSettingNamesAProvider()
	{
		_settings.Keys.ShouldNotContain(key => key.Contains("Provider", StringComparison.OrdinalIgnoreCase));
	}

	[Then(@"the call names the configured model and asks for the configured reasoning level")]
	public void ThenTheCallNamesModelAndReasoningLevel()
	{
		var body = SentBody();
		body.GetProperty("model").GetString().ShouldBe("gemini-3.7-flash");
		body.GetProperty("reasoning_effort").GetString().ShouldBe("medium");
	}

	[Then(@"the call asks the provider to answer with one object")]
	public void ThenTheCallAsksForJson()
	{
		SentBody().GetProperty("response_format").GetProperty("type").GetString().ShouldBe("json_object");
	}

	[Then(@"the call leaves the sampling temperature at the provider's default")]
	public void ThenNoTemperatureIsSent()
	{
		SentBody().TryGetProperty("temperature", out _).ShouldBeFalse();
	}

	// ── REQ-AI-023 ──────────────────────────────────────────────────────────

	[Given(@"the Worker has a model provider key")]
	public void GivenTheWorkerHasAKey()
	{
		_settings["AiChatClient:ApiKey"] = "synthetic-test-key";
	}

	[Given(@"its model configuration has a model no provider handler claims")]
	public void GivenAnUnclaimedModel()
	{
		_settings["AiChatClient:Model"] = "no-such-provider-model";
	}

	[Given(@"its model configuration has a blank model")]
	public void GivenABlankModel()
	{
		_settings["AiChatClient:Model"] = "";
	}

	[Given(@"its model configuration has a reasoning level other than low, medium, high")]
	public void GivenAnInvalidReasoningLevel()
	{
		_settings["AiChatClient:ReasoningEffort"] = "minimal";
	}

	[When(@"the Worker starts")]
	public async Task WhenTheWorkerStarts()
	{
		var builder = Host.CreateApplicationBuilder();
		builder.Configuration.Sources.Clear();
		builder.Configuration.AddInMemoryCollection(_settings);
		builder.Services.AddHpacSafetyAiSummarization(builder.Configuration);
		builder.Services.AddHostedService(_ => new ClaimLoopProbe(() => _claimLoopStarted = true));

		using var host = builder.Build();
		try
		{
			await host.StartAsync();
			await host.StopAsync();
		}
		catch (Exception exception) when (exception is OptionsValidationException or InvalidOperationException)
		{
			_startupFailure = exception;
		}
	}

	[Then(@"startup fails before any report is claimed")]
	public void ThenStartupFailsBeforeAnyClaim()
	{
		_startupFailure.ShouldNotBeNull();
		_startupFailure.Message.ShouldNotContain("synthetic-test-key");
		_claimLoopStarted.ShouldBeFalse();
	}

	// ── REQ-AI-024 ──────────────────────────────────────────────────────────

	[Given(@"the prompt version the Worker currently sends")]
	public void GivenTheCurrentPrompt()
	{
		OpenAiSummarizer.CurrentPromptVersion.ShouldBe("summarize-anonymize.v4");
	}

	[When(@"the prompt is read")]
	public void WhenThePromptIsRead()
	{
		_prompt = Collapse(File.ReadAllText(
			Path.Combine(AppContext.BaseDirectory, "Prompts", OpenAiSummarizer.CurrentPromptFileName)));
	}

	[Then(@"it states the rule that (.*)")]
	public void ThenItStatesTheRule(string rule)
	{
		PromptRules.ShouldContainKey(rule);
		foreach (var phrase in PromptRules[rule])
		{
			_prompt.ShouldContain(Collapse(phrase), Case.Sensitive, rule);
		}
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	/// <summary>
	///     One summarization attempt through the real registration, summarizer, and Gemini
	///     strategy, with only the HTTP transport replaced.
	/// </summary>
	private async Task<Exception?> Attempt(string completion)
	{
		_requestBody = null;
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(_settings).Build();

		var services = new ServiceCollection().AddHpacSafetyAiSummarization(configuration);
		services.AddHttpClient(GeminiHandler.HttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => new RecordingTransport(completion, (body, uri) => { _requestBody = body; _requestUri = uri; }));
		services.AddScoped<ISummarizer, OpenAiSummarizer>();

		await using var provider = services.BuildServiceProvider();
		var summarizer = provider.GetRequiredService<ISummarizer>();

		try
		{
			await summarizer.Summarize(
				SummarizationInput.Partition([
					new ClassifiedReportField(new SummarizationField("narrative", "What happened", "A synthetic landing."), false),
				]),
				CancellationToken.None);
			return null;
		}
		catch (SummarizationFailedException failure)
		{
			return failure;
		}
	}

	private JsonElement SentBody()
	{
		return JsonDocument.Parse(_requestBody.ShouldNotBeNull()).RootElement;
	}

	private static string Collapse(string text)
	{
		return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}

	/// <summary>Replies with one OpenAI-shaped completion and keeps the request body.</summary>
	private sealed class RecordingTransport(string completion,
											Action<string, Uri?> record) : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
																	 CancellationToken cancellationToken)
		{
			record(await request.Content!.ReadAsStringAsync(cancellationToken), request.RequestUri);

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(
					JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = completion } } } }),
					Encoding.UTF8,
					"application/json"),
			};
		}
	}

	/// <summary>Stands in for the Worker's claim loop: records whether the host ever started it.</summary>
	private sealed class ClaimLoopProbe(Action started) : IHostedService
	{
		public Task StartAsync(CancellationToken cancellationToken)
		{
			started();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
