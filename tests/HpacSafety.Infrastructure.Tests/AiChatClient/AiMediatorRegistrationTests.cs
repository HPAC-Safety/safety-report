using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.AiChatClient;

/// <summary>How the AI mediator is wired up, including the case that matters most in practice: no credential at all.</summary>
public class AiMediatorRegistrationTests
{
	[Fact]
	public void GivenNoConfigurationAtAll_WhenRegistered_ThenTheFailClosedDefaultIsUsed()
	{
		// Given — an ordinary local checkout
		using var provider = Provider([]);

		// When
		var client = provider.GetRequiredService<IAiMediator>();

		// Then
		client.ShouldBeOfType<AiMediator>();
		client.IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenKeyAndGeminiModel_WhenRegistered_ThenTheMediatorIsUsed()
	{
		// Given
		using var provider = Provider(Usable());

		// When
		var client = provider.GetRequiredService<IAiMediator>();

		// Then — no provider setting exists; the model name picks the strategy per call
		client.ShouldBeOfType<AiMediator>();
		client.IsConfigured.ShouldBeTrue();
	}

	[Fact]
	public async Task GivenGeminiModelInAnotherCase_WhenCompleted_ThenTheGeminiStrategyAnswers()
	{
		// Given
		var transport = new RecordingTransport();
		using var provider = Provider(Usable(("AiChatClient:Model", "Gemini-3.7-Flash")), transport);
		Should.NotThrow(() => Validate(provider));

		// When
		var answer = await provider.GetRequiredService<IAiMediator>().Complete(
			new AiChatRequest("Gemini-3.7-Flash", ReasoningEffort.Low, [new ChatMessage(ChatRole.User, "hi")]),
			CancellationToken.None);

		// Then — the request went to Gemini's OpenAI-compatible endpoint
		answer.ShouldBe("ok");
		transport.Uri!.Host.ShouldBe("generativelanguage.googleapis.com");
	}

	[Fact]
	public async Task GivenTwoCallersWithTheirOwnModels_WhenCompleted_ThenEachModelPicksItsOwnStrategy()
	{
		// Given — one key, the summary's model and the translator's model both gemini-
		var transport = new RecordingTransport();
		using var provider = Provider(Usable(), transport);
		var client = provider.GetRequiredService<IAiMediator>();

		// When
		await client.Complete(new AiChatRequest("gemini-3.5-pro", ReasoningEffort.High, [new ChatMessage(ChatRole.User, "a")]), CancellationToken.None);
		await client.Complete(new AiChatRequest("gemini-3.7-flash", ReasoningEffort.Low, [new ChatMessage(ChatRole.User, "b")]), CancellationToken.None);

		// Then
		transport.Models.ShouldBe(["gemini-3.5-pro", "gemini-3.7-flash"]);
	}

	[Fact]
	public async Task GivenAModelNoStrategyClaims_WhenCompleted_ThenRefusedWithoutSendingAnything()
	{
		// Given
		var transport = new RecordingTransport();
		using var provider = Provider(Usable(), transport);

		// When
		var failure = await Should.ThrowAsync<AiMediatorUnavailableException>(() =>
			provider.GetRequiredService<IAiMediator>().Complete(
				new AiChatRequest("claude-x", ReasoningEffort.Low, [new ChatMessage(ChatRole.User, "hi")]),
				CancellationToken.None));

		// Then — the message carries no model text, and no request left
		failure.Message.ShouldNotContain("claude-x");
		transport.Models.ShouldBeEmpty();
	}

	[Fact]
	public void GivenUsableConfiguration_WhenBound_ThenModelAndReasoningLevelAreRead()
	{
		// Given
		using var provider = Provider(Usable(("AiChatClient:ReasoningEffort", "Medium")));

		// When
		var options = provider.GetRequiredService<IOptions<AiChatClientOptions>>().Value;

		// Then
		options.Model.ShouldBe("gemini-3.7-flash");
		options.ReasoningEffort.ShouldBe(ReasoningEffort.Medium);
		Should.NotThrow(() => Validate(provider));
	}

	[Theory]
	[InlineData("AiChatClient:Model", "claude-x", "AiChatClient:Model")]
	[InlineData("AiChatClient:Model", "3.7-gemini", "AiChatClient:Model")]
	[InlineData("AiChatClient:Model", "", "Model")]
	[InlineData("AiChatClient:Model", "   ", "Model")]
	[InlineData("AiChatClient:ReasoningEffort", "", "ReasoningEffort")]
	[InlineData("AiChatClient:ReasoningEffort", "7", "ReasoningEffort")]
	public void GivenKeyWithUnusableSetting_WhenStartupValidates_ThenStartupFails(string key,
																				   string value,
																				   string named)
	{
		// Given
		using var provider = Provider(Usable((key, value)));

		// When
		var failure = Should.Throw<OptionsValidationException>(() => Validate(provider));

		// Then
		failure.Message.ShouldContain(named);
		failure.Message.ShouldNotContain("abc123");
	}

	[Theory]
	[InlineData("minimal")]
	[InlineData("bogus")]
	public void GivenKeyWithUnknownReasoningWord_WhenStartupValidates_ThenStartupFails(string value)
	{
		// Given — the binder refuses a word that is not a ReasoningEffort name
		using var provider = Provider(Usable(("AiChatClient:ReasoningEffort", value)));

		// When / Then
		Should.Throw<InvalidOperationException>(() => Validate(provider));
	}

	[Fact]
	public void GivenNoKeyAndAnUnclaimedModel_WhenStartupValidates_ThenPassesAndTheMediatorIsUnconfigured()
	{
		// Given — without a key the call is unavailable, as before, whatever the model
		using var provider = Provider(Usable(("AiChatClient:ApiKey", ""), ("AiChatClient:Model", "claude-x")));

		// When / Then
		Should.NotThrow(() => Validate(provider));
		provider.GetRequiredService<IAiMediator>().IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenNoKey_WhenStartupValidates_ThenPasses()
	{
		// Given — a local checkout: model committed, no key
		using var provider = Provider(new Dictionary<string, string?>
		{
			["AiChatClient:Model"] = "gemini-3.7-flash",
			["AiChatClient:ReasoningEffort"] = "low",
		});

		// When / Then
		Should.NotThrow(() => Validate(provider));
		provider.GetRequiredService<IAiMediator>().IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void GivenNullArguments_WhenRegistered_ThenRefused()
	{
		// Given / When / Then
		Should.Throw<ArgumentNullException>(() =>
			AiChatClientServiceCollectionExtensions.AddHpacSafetyAiSummarization(null!, new ConfigurationBuilder().Build()));

		Should.Throw<ArgumentNullException>(() =>
			new ServiceCollection().AddHpacSafetyAiSummarization(null!));
	}

	private static Dictionary<string, string?> Usable(params (string Key, string? Value)[] overrides)
	{
		var settings = new Dictionary<string, string?>
		{
			["AiChatClient:ApiKey"] = "abc123",
			["AiChatClient:Model"] = "gemini-3.7-flash",
			["AiChatClient:ReasoningEffort"] = "low",
		};

		foreach (var (key, value) in overrides)
		{
			settings[key] = value;
		}

		return settings;
	}

	/// <summary>What the host runs at startup for every <c>ValidateOnStart</c> registration.</summary>
	private static void Validate(ServiceProvider provider)
	{
		provider.GetRequiredService<IStartupValidator>().Validate();
	}

	private static ServiceProvider Provider(Dictionary<string, string?> settings,
											HttpMessageHandler? transport = null)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
		var services = new ServiceCollection().AddHpacSafetyAiSummarization(configuration);

		if (transport is not null)
		{
			// Last registration wins: the strategy's named client is answered in-process.
			services.AddSingleton<IHttpClientFactory>(new StubClientFactory(transport));
		}

		return services.BuildServiceProvider();
	}

	private sealed class RecordingTransport : HttpMessageHandler
	{
		public Uri? Uri { get; private set; }

		public List<string> Models { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
																	 CancellationToken cancellationToken)
		{
			Uri = request.RequestUri;
			using var body = System.Text.Json.JsonDocument.Parse(
				await request.Content!.ReadAsStringAsync(cancellationToken));
			Models.Add(body.RootElement.GetProperty("model").GetString()!);

			return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
			{
				Content = new StringContent(
					"""{"choices":[{"message":{"content":"ok"}}]}""",
					System.Text.Encoding.UTF8,
					"application/json"),
			};
		}
	}

	private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(handler, disposeHandler: false);
		}
	}
}

/// <summary>
///     <see cref="AiMediator" /> fails closed: it never silently proceeds without a key.
/// </summary>
public class AiMediatorFailClosedTests
{
	[Fact]
	public async Task GivenNoKey_WhenACompletionIsRequested_ThenRefusedWithoutReachingAnyHandler()
	{
		// Given
		var handler = new CountingHandler();
		var mediator = new AiMediator([handler], Options.Create(new AiChatClientOptions()));

		// When / Then
		await Should.ThrowAsync<AiMediatorUnavailableException>(() =>
			mediator.Complete(
				new AiChatRequest("gemini-any", ReasoningEffort.Low, [new ChatMessage(ChatRole.User, "hello")]),
				CancellationToken.None));
		handler.Calls.ShouldBe(0);
	}

	[Fact]
	public async Task GivenTwoHandlers_WhenACompletionIsRequested_ThenTheModelPrefixPicksOne()
	{
		// Given — a second, hypothetical OpenAI-compatible handler is one class and one registration
		var gemini = new CountingHandler("gemini-");
		var other = new CountingHandler("gpt-");
		var mediator = new AiMediator([gemini, other], Options.Create(new AiChatClientOptions { ApiKey = "k" }));

		// When
		await mediator.Complete(
			new AiChatRequest("gpt-5", null, [new ChatMessage(ChatRole.User, "hello")], AiResponseFormat.Text),
			CancellationToken.None);

		// Then
		other.Calls.ShouldBe(1);
		gemini.Calls.ShouldBe(0);
	}

	private sealed class CountingHandler(string prefix = "gemini-") : IAiHandler
	{
		public int Calls { get; private set; }

		public string ModelPrefix => prefix;

		public Task<string> Complete(AiChatRequest request,
									 CancellationToken cancellationToken)
		{
			Calls++;
			return Task.FromResult("ok");
		}
	}
}

/// <summary>
///     <see cref="AiMediatorUnavailableException" /> is what every mediator
///     failure becomes, and its message is the one thing a caller is allowed to show.
/// </summary>
public class AiMediatorUnavailableExceptionTests
{
	[Fact]
	public void GivenNoMessage_WhenCreated_ThenStillSaysSomethingUsable()
	{
		// Given / When
		var cause = new AiMediatorUnavailableException();

		// Then
		cause.Message.ShouldBe("The AI mediator is unavailable.");
	}

	[Fact]
	public void GivenUnderlyingFailure_WhenWrapped_ThenCauseIsKeptButNotMessage()
	{
		// Given
		var underlying = new HttpRequestException("connection refused with key abc123");

		// When
		var cause = new AiMediatorUnavailableException("The provider could not be reached.", underlying);

		// Then
		cause.Message.ShouldBe("The provider could not be reached.");
		cause.Message.ShouldNotContain("abc123");
		cause.InnerException.ShouldBe(underlying);
	}
}
