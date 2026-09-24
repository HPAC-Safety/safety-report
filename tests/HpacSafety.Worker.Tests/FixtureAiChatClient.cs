using HpacSafety.Core;

namespace HpacSafety.Worker.Tests;

/// <summary>
///     A deterministic, controlled <see cref="IAiChatClient" /> double for tests —
///     issue #20's "deterministic controlled provider fixtures for tests." Returns
///     one canned response per instance and records what it was called with.
/// </summary>
internal sealed class FixtureAiChatClient : IAiChatClient
{
	private readonly string _response;

	public FixtureAiChatClient(string response,
							   bool isConfigured = true)
	{
		_response = response;
		IsConfigured = isConfigured;
	}

	public bool IsConfigured { get; }

	public int CallCount { get; private set; }

	public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

	public string? LastModel { get; private set; }

	public ReasoningEffort? LastReasoningEffort { get; private set; }

	public Task<string> Complete(AiChatRequest request,
								 CancellationToken cancellationToken)
	{
		CallCount++;
		LastModel = request.Model;
		LastReasoningEffort = request.ReasoningEffort;
		LastMessages = request.Messages;

		if (!IsConfigured)
		{
			throw new AiChatClientUnavailableException("Fixture is not configured.");
		}

		return Task.FromResult(_response);
	}
}
