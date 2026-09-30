using System.Text.Json;
using System.Text.RegularExpressions;
using HpacSafety.Core;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     The one <see cref="ITranslator" />: Gemini, asked through the same
///     <see cref="IAiChatClient" /> strategy and key the summary call uses, with its own
///     model and reasoning level (ADR-0179).
/// </summary>
/// <remarks>
///     <para>
///         This is a separate call from the summary, outside "one model call, only
///         with consent". The request carries the current translation prompt (with the
///         term list, ADR-0102) and the strings to translate, and nothing else: no
///         report identifier, no other answer, no report context.
///     </para>
///     <para>
///         A model, unlike a translation API, can chatter or drop a field, so the
///         contract is enforced here: the reply must be
///         <c>{"translations": [...]}</c> with one string per input, in order, none
///         empty unless its input was, each keeping every <c>{placeholder}</c> and
///         markup tag it was given. Anything else is refused with a fixed message —
///         never the reply, the text, or the credential.
///     </para>
///     <para>
///         It mirrors <c>tools/translator.mjs</c>, which sends the same prompt file for
///         the CI locale job.
///     </para>
/// </remarks>
public sealed partial class AiChatTranslator : ITranslator
{
	private readonly IAiChatClient _chat;
	private readonly TranslationOptions _options;

	/// <summary>Creates the translator.</summary>
	/// <param name="chat">The provider strategy, which holds the key.</param>
	/// <param name="options">Translation's own model and reasoning level.</param>
	public AiChatTranslator(IAiChatClient chat,
							IOptions<TranslationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(chat);
		ArgumentNullException.ThrowIfNull(options);

		_chat = chat;
		_options = options.Value;
	}

	/// <inheritdoc />
	public bool IsConfigured => _chat.IsConfigured;

	/// <inheritdoc />
	public async Task<IReadOnlyList<string>> Translate(
		IReadOnlyList<string> texts,
		Locale source,
		Locale target,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(texts);

		if (!IsConfigured)
		{
			throw new TranslationUnavailableException(
				"Translation is not configured on this server.");
		}

		if (source == target)
		{
			throw new TranslationUnavailableException(
				"A translation needs two different languages.");
		}

		if (texts.Count == 0)
		{
			return [];
		}

		var request = new AiChatRequest(
			_options.Model!,
			_options.ParsedReasoningEffort!.Value,
			[
				new ChatMessage(ChatRole.System, TranslationPrompt.Render(source, target)),
				new ChatMessage(ChatRole.User, JsonSerializer.Serialize(new { texts })),
			]);

		string reply;

		try
		{
			reply = await _chat.Complete(request, cancellationToken).ConfigureAwait(false);
		}
		catch (AiChatClientUnavailableException cause)
		{
			// The cause's message is written to be safe to show: a status, or a
			// fixed sentence. Never the provider's body.
			throw new TranslationUnavailableException($"The translation service failed. {cause.Message}", cause);
		}

		return Parse(reply, texts);
	}

	/// <summary>Reads the reply against what was sent, or refuses it.</summary>
	private static List<string> Parse(string reply,
											   IReadOnlyList<string> texts)
	{
		IReadOnlyList<string?>? translations;

		try
		{
			using var document = JsonDocument.Parse(Unfence(reply));

			translations = document.RootElement.ValueKind == JsonValueKind.Object
						   && document.RootElement.TryGetProperty("translations", out var array)
						   && array.ValueKind == JsonValueKind.Array
				? [.. array.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)]
				: null;
		}
		catch (JsonException cause)
		{
			throw new TranslationUnavailableException("The translation service returned something unreadable.", cause);
		}

		// The reply is positional, with no keys, so a length mismatch would
		// silently shift every field by one — French help text landing in the
		// label. Refusing is the only safe answer.
		if (translations is null
			|| translations.Count != texts.Count)
		{
			throw new TranslationUnavailableException(
				"The translation service returned a different number of results than were sent.");
		}

		var result = new List<string>(texts.Count);

		for (var index = 0; index < texts.Count; index++)
		{
			var translated = translations[index];

			if (translated is null
				|| (!string.IsNullOrWhiteSpace(texts[index]) && string.IsNullOrWhiteSpace(translated)))
			{
				throw new TranslationUnavailableException("The translation service returned an empty translation.");
			}

			if (!TokensOf(texts[index]).SequenceEqual(TokensOf(translated), StringComparer.Ordinal))
			{
				throw new TranslationUnavailableException(
					"The translation service changed a placeholder or a markup tag.");
			}

			result.Add(translated);
		}

		return result;
	}

	/// <summary>
	///     Strips a markdown fence, if the model wrapped one round its JSON. A fenced
	///     reply is a correct reply in decoration; anything that is not JSON fails.
	/// </summary>
	private static string Unfence(string reply)
	{
		var text = reply.Trim();
		var match = FencePattern().Match(text);

		return match.Success ? match.Groups[1].Value.Trim() : text;
	}

	/// <summary>Every <c>{placeholder}</c> and markup tag in a string, sorted.</summary>
	private static List<string> TokensOf(string text)
	{
		return [.. ProtectedTokenPattern().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal)];
	}

	[GeneratedRegex(@"\{[^{}]*\}|</?[A-Za-z][^<>]*>")]
	private static partial Regex ProtectedTokenPattern();

	[GeneratedRegex(@"^```(?:json)?\s*\n([\s\S]*?)\n?```$")]
	private static partial Regex FencePattern();
}
