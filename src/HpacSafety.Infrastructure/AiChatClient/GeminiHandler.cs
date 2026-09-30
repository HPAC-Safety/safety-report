using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HpacSafety.Core;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     The handler for Google Gemini models (<c>gemini-*</c>), the first reviewed
///     <see cref="IAiHandler" />, reached through Gemini's OpenAI-compatible
///     chat-completions endpoint rather than a Gemini-specific SDK — the request is
///     already shaped to that spec, so no translation layer sits between the two.
/// </summary>
/// <remarks>
///     This is the only type in the repository that talks to Gemini. Everything above
///     it depends on <see cref="IAiMediator" />. It sends the reasoning level, when one is
///     given, as <c>reasoning_effort</c>, which Gemini maps to its thinking level, and a JSON
///     object response format when one is asked for; it never sends a temperature, because
///     Google recommends leaving Gemini 3 at its default (ADR-0104). The key is the one
///     <c>AiChatClient:ApiKey</c> every handler shares; <c>AiChatClient:Endpoint</c>
///     overrides the default endpoint.
/// </remarks>
public sealed class GeminiHandler : IAiHandler
{
	/// <summary>The named <see cref="HttpClient" /> this resolves.</summary>
	public const string HttpClientName = "gemini";

	private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

	private readonly IHttpClientFactory _clients;
	private readonly Func<AiChatClientOptions> _options;

	/// <summary>Creates the handler.</summary>
	/// <param name="clients">Supplies the named HTTP client.</param>
	/// <param name="options">
	///     Reads the provider configuration when a call is made, not at construction, so the
	///     startup validators can ask which models this handler claims while the options
	///     are still being bound.
	/// </param>
	public GeminiHandler(IHttpClientFactory clients,
						 Func<AiChatClientOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_clients = clients;
		_options = options;
	}

	/// <inheritdoc />
	public string ModelPrefix => "gemini-";

	private bool IsConfigured => !string.IsNullOrWhiteSpace(_options().ApiKey);

	/// <inheritdoc />
	public async Task<string> Complete(AiChatRequest request,
									   CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!IsConfigured)
		{
			throw new AiMediatorUnavailableException("No AI chat provider is configured and approved for use.");
		}

		var body = new GeminiRequest(
			request.Model,
			[.. request.Messages.Select(ToGeminiMessage)],
			request.ReasoningEffort is { } effort ? ToReasoningEffort(effort) : null,
			request.ResponseFormat == AiResponseFormat.JsonObject ? new GeminiResponseFormat("json_object") : null);

		GeminiResponse? payload;

		try
		{
			using var client = _clients.CreateClient(HttpClientName);
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options().ApiKey);

			using var response = await client
				.PostAsJsonAsync(ResolvedEndpoint(), body, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				// The status only. A provider error body is not something this
				// exception's message is a safe place to carry — see
				// AiMediatorUnavailableException's own remarks.
				throw new AiMediatorUnavailableException(
					$"The AI chat provider answered {(int)response.StatusCode}.");
			}

			payload = await response.Content
				.ReadFromJsonAsync<GeminiResponse>(cancellationToken)
				.ConfigureAwait(false);
		}
		catch (HttpRequestException cause)
		{
			throw new AiMediatorUnavailableException("The AI chat provider could not be reached.", cause);
		}
		catch (JsonException cause)
		{
			throw new AiMediatorUnavailableException("The AI chat provider returned something unreadable.", cause);
		}

		var choices = payload?.Choices;
		var content = choices is { Count: > 0 } ? choices[0].Message?.Content : null;

		if (string.IsNullOrWhiteSpace(content))
		{
			throw new AiMediatorUnavailableException("The AI chat provider returned no completion.");
		}

		return content;
	}

	/// <summary>The configured host, unless the default OpenAI-compatible endpoint applies.</summary>
	private string ResolvedEndpoint()
	{
		var endpoint = _options().Endpoint;
		return string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint;
	}

	private static GeminiMessage ToGeminiMessage(ChatMessage message)
	{
		return new GeminiMessage(message.Role == ChatRole.System ? "system" : "user", message.Content);
	}

	private static string ToReasoningEffort(ReasoningEffort effort)
	{
		return effort switch
		{
			ReasoningEffort.Low => "low",
			ReasoningEffort.Medium => "medium",
			ReasoningEffort.High => "high",
			_ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Not a reasoning level."),
		};
	}

	private sealed record GeminiRequest(
		[property: JsonPropertyName("model")] string Model,
		[property: JsonPropertyName("messages")]
		IReadOnlyList<GeminiMessage> Messages,
		[property: JsonPropertyName("reasoning_effort"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		string? ReasoningEffort,
		[property: JsonPropertyName("response_format"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		GeminiResponseFormat? ResponseFormat);

	private sealed record GeminiResponseFormat(
		[property: JsonPropertyName("type")] string Type);

	private sealed record GeminiMessage(
		[property: JsonPropertyName("role")] string Role,
		[property: JsonPropertyName("content")]
		string Content);

	private sealed record GeminiResponse(
		[property: JsonPropertyName("choices")]
		IReadOnlyList<GeminiChoice>? Choices);

	private sealed record GeminiChoice(
		[property: JsonPropertyName("message")]
		GeminiResponseMessage? Message);

	private sealed record GeminiResponseMessage(
		[property: JsonPropertyName("content")]
		string? Content);
}
