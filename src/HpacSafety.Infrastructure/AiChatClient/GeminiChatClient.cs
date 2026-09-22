using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HpacSafety.Core;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     Google Gemini, the first reviewed <see cref="IAiChatClient" /> concretion,
///     reached through its OpenAI-compatible chat-completions endpoint rather than
///     a Gemini-specific SDK — the port is already shaped to that spec, so no
///     translation layer sits between the two.
/// </summary>
/// <remarks>
///     This is the only type in the repository that names Gemini on the server
///     side. Everything above it depends on <see cref="IAiChatClient" />.
/// </remarks>
public sealed class GeminiChatClient : IAiChatClient
{
	/// <summary>The named <see cref="HttpClient" /> this resolves.</summary>
	public const string HttpClientName = "gemini";

	private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

	private readonly IHttpClientFactory _clients;
	private readonly GeminiOptions _options;

	/// <summary>Creates the client.</summary>
	/// <param name="clients">Supplies the named HTTP client.</param>
	/// <param name="options">Provider configuration.</param>
	public GeminiChatClient(IHttpClientFactory clients, IOptions<GeminiOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_clients = clients;
		_options = options.Value;
	}

	/// <inheritdoc />
	public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

	/// <inheritdoc />
	public async Task<string> Complete(string model, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(messages);

		if (!IsConfigured)
		{
			throw new AiChatClientUnavailableException("No AI chat provider is configured and approved for use.");
		}

		var request = new GeminiRequest(model, [.. messages.Select(ToGeminiMessage)]);

		GeminiResponse? payload;

		try
		{
			using var client = _clients.CreateClient(HttpClientName);
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

			using var response = await client
				.PostAsJsonAsync(ResolvedEndpoint(), request, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				// The status only. A provider error body is not something this
				// exception's message is a safe place to carry — see
				// AiChatClientUnavailableException's own remarks.
				throw new AiChatClientUnavailableException(
					$"The AI chat provider answered {(int)response.StatusCode}.");
			}

			payload = await response.Content
				.ReadFromJsonAsync<GeminiResponse>(cancellationToken)
				.ConfigureAwait(false);
		}
		catch (HttpRequestException cause)
		{
			throw new AiChatClientUnavailableException("The AI chat provider could not be reached.", cause);
		}
		catch (JsonException cause)
		{
			throw new AiChatClientUnavailableException("The AI chat provider returned something unreadable.", cause);
		}

		var choices = payload?.Choices;
		var content = choices is { Count: > 0 } ? choices[0].Message?.Content : null;

		if (string.IsNullOrWhiteSpace(content))
		{
			throw new AiChatClientUnavailableException("The AI chat provider returned no completion.");
		}

		return content;
	}

	/// <summary>The configured host, unless the default OpenAI-compatible endpoint applies.</summary>
	private string ResolvedEndpoint()
	{
		return string.IsNullOrWhiteSpace(_options.Endpoint) ? DefaultEndpoint : _options.Endpoint;
	}

	private static GeminiMessage ToGeminiMessage(ChatMessage message)
	{
		return new GeminiMessage(message.Role == ChatRole.System ? "system" : "user", message.Content);
	}

	private sealed record GeminiRequest(
		[property: JsonPropertyName("model")] string Model,
		[property: JsonPropertyName("messages")]
		IReadOnlyList<GeminiMessage> Messages);

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
