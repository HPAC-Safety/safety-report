using System.Text.Json;
using System.Text.Json.Serialization;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Options;

namespace HpacSafety.Worker.Summarization;

/// <summary>
///     The one concrete <see cref="ISummarizer" />: loads the current versioned prompt,
///     applies the deterministic marking pass, makes exactly one
///     <see cref="IAiChatClient" /> call, and strictly validates the response.
/// </summary>
public sealed class PromptDrivenSummarizer : ISummarizer
{
	/// <summary>The current prompt file under <c>Prompts/</c>. Bump on any behavior change.</summary>
	public const string CurrentPromptFileName = "summarize-anonymize.v2.md";

	/// <summary>The provenance value stamped on every summary this prompt produces.</summary>
	public static readonly string CurrentPromptVersion = Path.GetFileNameWithoutExtension(CurrentPromptFileName);

	private static readonly JsonSerializerOptions RequestSerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
	};

	private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
	};

	private readonly IAiChatClient _aiChatClient;
	private readonly string _model;
	private readonly string _promptsDirectory;
	private string? _cachedPrompt;

	public PromptDrivenSummarizer(IAiChatClient aiChatClient,
								  IOptions<AiChatClientOptions> options)
	{
		ArgumentNullException.ThrowIfNull(aiChatClient);
		ArgumentNullException.ThrowIfNull(options);

		_aiChatClient = aiChatClient;
		_model = options.Value.Model ?? string.Empty;
		_promptsDirectory = Path.Combine(AppContext.BaseDirectory, "Prompts");
	}

	/// <inheritdoc />
	public async Task<SummaryDraft> Summarize(SummarizationInput input,
											  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);

		var marked = PrivateValueMarker.Mark(input);
		var systemPrompt = LoadPrompt();
		var userMessage = BuildUserMessage(marked);

		string response;
		try
		{
			response = await _aiChatClient.Complete(
				_model,
				[new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userMessage)],
				cancellationToken).ConfigureAwait(false);
		}
		catch (AiChatClientUnavailableException exception)
		{
			throw new SummarizationFailedException("The AI chat provider was unavailable for this summarization attempt.", exception);
		}

		var (textEn, textFr) = ParseStrictResponse(response);
		return new SummaryDraft(textEn, textFr, _model, CurrentPromptVersion);
	}

	private string LoadPrompt()
	{
		return _cachedPrompt ??= File.ReadAllText(Path.Combine(_promptsDirectory, CurrentPromptFileName));
	}

	private static string BuildUserMessage(SummarizationInput input)
	{
		var payload = new SummarizationRequestPayload(
			[.. input.ReportContent.Select(ToRequestField)],
			[.. input.PrivateContext.Select(ToRequestField)]);

		return JsonSerializer.Serialize(payload, RequestSerializerOptions);
	}

	private static SummarizationRequestField ToRequestField(SummarizationField field)
	{
		return new SummarizationRequestField(field.QuestionKey, field.Label, field.Value);
	}

	private static (string TextEn, string TextFr) ParseStrictResponse(string response)
	{
		SummarizationResponsePayload? payload;
		try
		{
			payload = JsonSerializer.Deserialize<SummarizationResponsePayload>(response, ResponseSerializerOptions);
		}
		catch (JsonException exception)
		{
			throw new SummarizationFailedException("The AI chat provider's response was not valid JSON.", exception);
		}

		if (payload is null
			|| string.IsNullOrWhiteSpace(payload.AiSummaryEn)
			|| string.IsNullOrWhiteSpace(payload.AiSummaryFr))
		{
			throw new SummarizationFailedException(
				"The AI chat provider's response did not contain two nonblank summary fields.");
		}

		return (payload.AiSummaryEn, payload.AiSummaryFr);
	}

	private sealed record SummarizationRequestField(string QuestionKey, string Label, string Value);

	private sealed record SummarizationRequestPayload(
		IReadOnlyList<SummarizationRequestField> ReportContent,
		IReadOnlyList<SummarizationRequestField> PrivateContext);

	[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
	private sealed record SummarizationResponsePayload(string? AiSummaryEn, string? AiSummaryFr);
}
