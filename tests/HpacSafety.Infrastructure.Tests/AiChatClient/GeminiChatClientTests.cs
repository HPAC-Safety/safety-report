using System.Net;
using System.Text;
using System.Text.Json;
using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.AiChatClient;

/// <summary>
///     The Gemini adapter, against a stubbed transport. Nothing here reaches the
///     network, and no real credential is used.
/// </summary>
public class GeminiChatClientTests
{
	private const string Key = "test-key";

	[Fact]
	public async Task GivenNoCredential_WhenACompletionIsRequested_ThenReportsUnconfigured()
	{
		// Given
		var (client, _) = Client(apiKey: null);

		// Then
		client.IsConfigured.ShouldBeFalse();

		await Should.ThrowAsync<AiChatClientUnavailableException>(() =>
			client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None));
	}

	[Fact]
	public async Task GivenMessages_WhenSent_ThenRequestNamesTheModelAndCarriesRolesInOrder()
	{
		// Given
		var (client, transport) = Client(Responds("hello back"));

		// When
		await client.Complete(Request([new ChatMessage(ChatRole.System, "You are terse."), new ChatMessage(ChatRole.User, "Summarize this.")]), CancellationToken.None);

		// Then
		var sent = transport.LastBody();
		sent.GetProperty("model").GetString().ShouldBe("gemini-3.7-flash");

		var messages = sent.GetProperty("messages");
		messages[0].GetProperty("role").GetString().ShouldBe("system");
		messages[0].GetProperty("content").GetString().ShouldBe("You are terse.");
		messages[1].GetProperty("role").GetString().ShouldBe("user");
		messages[1].GetProperty("content").GetString().ShouldBe("Summarize this.");
	}

	[Theory]
	[InlineData(ReasoningEffort.Low, "low")]
	[InlineData(ReasoningEffort.Medium, "medium")]
	[InlineData(ReasoningEffort.High, "high")]
	public async Task GivenReasoningLevel_WhenSent_ThenRequestCarriesItAsReasoningEffort(ReasoningEffort effort,
																						   string expected)
	{
		// Given
		var (client, transport) = Client(Responds("ok"));

		// When
		await client.Complete(Request([new ChatMessage(ChatRole.User, "hello")], effort), CancellationToken.None);

		// Then
		transport.LastBody().GetProperty("reasoning_effort").GetString().ShouldBe(expected);
	}

	[Fact]
	public async Task GivenAnyRequest_WhenSent_ThenAsksForJsonObjectAndLeavesTemperatureAtDefault()
	{
		// Given
		var (client, transport) = Client(Responds("ok"));

		// When
		await client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None);

		// Then — Google recommends Gemini 3's default temperature (ADR-0104)
		var sent = transport.LastBody();
		sent.GetProperty("response_format").GetProperty("type").GetString().ShouldBe("json_object");
		sent.TryGetProperty("temperature", out _).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenUndefinedReasoningLevel_WhenSent_ThenRefusedBeforeAnythingIsSent()
	{
		// Given
		var (client, transport) = Client(Responds("ok"));

		// When / Then
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
			client.Complete(Request([new ChatMessage(ChatRole.User, "hello")], (ReasoningEffort)42), CancellationToken.None));
		transport.Requests.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenAValidResponse_WhenParsed_ThenTheFirstChoicesTextIsReturned()
	{
		// Given
		var (client, _) = Client(Responds("the completion text"));

		// When
		var completion = await client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None);

		// Then
		completion.ShouldBe("the completion text");
	}

	[Fact]
	public async Task GivenCredential_WhenSent_ThenSentAsABearerToken()
	{
		// Given
		var (client, transport) = Client(Responds("ok"));

		// When
		await client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None);

		// Then
		var authorization = transport.Requests[0].Headers.Authorization;
		authorization!.Scheme.ShouldBe("Bearer");
		authorization.Parameter.ShouldBe(Key);
	}

	[Fact]
	public async Task GivenNoEndpointConfigured_WhenSent_ThenTheDefaultOpenAiCompatibleHostIsUsed()
	{
		// Given
		var (client, transport) = Client(Responds("ok"));

		// When
		await client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None);

		// Then
		transport.Requests[0].RequestUri!.Host.ShouldBe("generativelanguage.googleapis.com");
	}

	[Fact]
	public async Task GivenConfiguredEndpoint_WhenSent_ThenOverridesTheDefaultHost()
	{
		// Given
		var (client, transport) = Client(Responds("ok"), endpoint: "https://gemini.example.invalid/openai/chat/completions");

		// When
		await client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None);

		// Then
		transport.Requests[0].RequestUri!.Host.ShouldBe("gemini.example.invalid");
	}

	[Fact]
	public async Task GivenProviderRefuses_WhenCompleted_ThenFailureCarriesNoProviderBody()
	{
		// Given — a provider error body can echo the submitted prompt back
		var (client, _) = Client(new StubTransport(
			new HttpResponseMessage(HttpStatusCode.Forbidden)
			{
				Content = new StringContent("{\"error\":\"invalid key, prompt was: hello\"}"),
			}));

		// When
		var cause = await Should.ThrowAsync<AiChatClientUnavailableException>(() =>
			client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None));

		// Then
		cause.Message.ShouldContain("403");
		cause.Message.ShouldNotContain("hello");
		cause.Message.ShouldNotContain(Key);
	}

	[Fact]
	public async Task GivenProviderCannotBeReached_WhenCalled_ThenFailureIsReportedSafely()
	{
		// Given
		var (client, _) = Client(new StubTransport(new HttpRequestException("no route to host")));

		// When
		var cause = await Should.ThrowAsync<AiChatClientUnavailableException>(() =>
			client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None));

		// Then
		cause.Message.ShouldBe("The AI chat provider could not be reached.");
		cause.Message.ShouldNotContain(Key);
	}

	[Fact]
	public async Task GivenProviderAnswersWithNoChoicesAtAll_WhenParsed_ThenRefused()
	{
		// Given — a 200 with a body that carries no choices array
		var (client, _) = Client(new StubTransport(
			new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{}", Encoding.UTF8, "application/json"),
			}));

		// When / Then
		await Should.ThrowAsync<AiChatClientUnavailableException>(() =>
			client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None));
	}

	[Fact]
	public async Task GivenProviderAnswersWithNonsense_WhenParsed_ThenRefused()
	{
		// Given
		var (client, _) = Client(new StubTransport(
			new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json at all") }));

		// When / Then
		await Should.ThrowAsync<AiChatClientUnavailableException>(() =>
			client.Complete(Request([new ChatMessage(ChatRole.User, "hello")]), CancellationToken.None));
	}

	private static AiChatRequest Request(IReadOnlyList<ChatMessage> messages,
										 ReasoningEffort reasoningEffort = ReasoningEffort.Low)
	{
		return new AiChatRequest("gemini-3.7-flash", reasoningEffort, messages);
	}

	private static StubTransport Responds(string completion)
	{
		return new StubTransport(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(
				JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = completion } } } }),
				Encoding.UTF8,
				"application/json"),
		});
	}

	private static (GeminiChatClient Client, StubTransport Transport) Client(
		StubTransport? transport = null,
		string? apiKey = Key,
		string? endpoint = null)
	{
		transport ??= Responds("ok");

		var options = Options.Create(new AiChatClientOptions
		{
			ApiKey = apiKey,
			Endpoint = endpoint,
		});

		return (new GeminiChatClient(new StubClientFactory(transport), options), transport);
	}

	/// <summary>Captures what was sent and replays a canned response.</summary>
	private sealed class StubTransport : HttpMessageHandler
	{
		private readonly List<string> _bodies = [];
		private readonly Exception? _failure;
		private readonly HttpResponseMessage? _response;

		public StubTransport(HttpResponseMessage response)
		{
			_response = response;
		}

		public StubTransport(Exception failure)
		{
			_failure = failure;
		}

		public List<HttpRequestMessage> Requests { get; } = [];

		public JsonElement LastBody()
		{
			return JsonDocument.Parse(_bodies[^1]).RootElement;
		}

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			Requests.Add(request);

			if (request.Content is not null)
			{
				_bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
			}

			return _failure is not null ? throw _failure : _response!;
		}
	}

	private sealed class StubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(handler, false);
		}
	}
}
