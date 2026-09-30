using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>
///     Refuses a key that names no registered provider. Run by every host that holds
///     the Gemini key, the API (translation) as well as the Worker (summaries and
///     translation), so a typo cannot silently fall back to the fail-closed client
///     while a key sits unused.
/// </summary>
internal sealed class AiChatProviderOptionsValidator : IValidateOptions<AiChatClientOptions>
{
	public ValidateOptionsResult Validate(string? name,
										  AiChatClientOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (string.IsNullOrWhiteSpace(options.ApiKey)
			|| AiChatClientServiceCollectionExtensions.IsKnownProvider(options.Provider))
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			$"{AiChatClientOptions.SectionName}:Provider must name a registered provider "
			+ $"({string.Join(", ", AiChatClientServiceCollectionExtensions.KnownProviders)}).");
	}
}
