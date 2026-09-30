using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     Refuses a translation model or reasoning level that could not make a usable
///     call, at startup, key or no key: a blank model would otherwise surface only
///     as failed outbox messages (REQ-WLD-040). With a key held, a model no provider handler
///     claims is refused too, because the model name alone picks the provider (REQ-WLD-043);
///     with no key translation is simply unavailable, as before.
/// </summary>
internal sealed class TranslationOptionsValidator(IOptions<AiChatClientOptions> chat,
													IEnumerable<IAiHandler> handlers)
	: IValidateOptions<TranslationOptions>
{
	public ValidateOptionsResult Validate(string? name,
										  TranslationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.Model))
		{
			failures.Add($"{TranslationOptions.SectionName}:{nameof(TranslationOptions.Model)} must name a model.");
		}
		else if (!string.IsNullOrWhiteSpace(chat.Value.ApiKey)
				 && AiMediator.HandlerFor(handlers, options.Model) is null)
		{
			failures.Add(AiMediator.Unclaimed(
				handlers,
				$"{TranslationOptions.SectionName}:{nameof(TranslationOptions.Model)}"));
		}

		if (options.ParsedReasoningEffort is null)
		{
			failures.Add(
				$"{TranslationOptions.SectionName}:{nameof(TranslationOptions.ReasoningEffort)} must be low, medium, or high.");
		}

		return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
	}
}
