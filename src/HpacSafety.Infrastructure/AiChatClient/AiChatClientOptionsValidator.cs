using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     Refuses a summarization configuration that holds a key but could not make a
///     usable call: a blank model or an undefined reasoning level (REQ-AI-023).
///     Registered with <c>ValidateOnStart</c> by <c>AddHpacSafetyAiChatClient</c>, so
///     the Worker fails before any report is claimed rather than sending an empty model
///     name. The provider check is <see cref="AiChatProviderOptionsValidator" />, which
///     every host that holds the key runs; translation has its own model and effort
///     (<c>TranslationOptions</c>, ADR-0179) and never reads these two.
/// </summary>
internal sealed class AiChatClientOptionsValidator : IValidateOptions<AiChatClientOptions>
{
	public ValidateOptionsResult Validate(string? name,
										  AiChatClientOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// No key is the fail-closed state, not a misconfiguration: a local checkout
		// runs, and every summarization attempt reports the provider unavailable.
		if (string.IsNullOrWhiteSpace(options.ApiKey))
		{
			return ValidateOptionsResult.Success;
		}

		var failures = new List<string>();

		if (string.IsNullOrWhiteSpace(options.Model))
		{
			failures.Add($"{AiChatClientOptions.SectionName}:Model must name a model.");
		}

		if (options.ReasoningEffort is not { } effort || !Enum.IsDefined(effort))
		{
			failures.Add($"{AiChatClientOptions.SectionName}:ReasoningEffort must be low, medium, or high.");
		}

		return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
	}
}
