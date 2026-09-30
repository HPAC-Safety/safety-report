using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Translation;

/// <summary>
///     Refuses a translation model or reasoning level that could not make a usable
///     call, at startup, key or no key: a blank model would otherwise surface only
///     as failed outbox messages (REQ-WLD-040).
/// </summary>
internal sealed class TranslationOptionsValidator : IValidateOptions<TranslationOptions>
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

		if (options.ParsedReasoningEffort is null)
		{
			failures.Add(
				$"{TranslationOptions.SectionName}:{nameof(TranslationOptions.ReasoningEffort)} must be low, medium, or high.");
		}

		return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
	}
}
