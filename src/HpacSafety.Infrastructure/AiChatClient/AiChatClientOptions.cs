namespace HpacSafety.Infrastructure.AiChatClient;

/// <summary>Configuration for whichever <c>IAiChatClient</c> concretion is registered.</summary>
public sealed class AiChatClientOptions
{
	/// <summary>The configuration section this binds to.</summary>
	public const string SectionName = "AiChatClient";

	/// <summary>The provider-specific model identifier to request completions from.</summary>
	public string? Model { get; set; }
}
