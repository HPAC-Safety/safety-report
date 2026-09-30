using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.AiChatClient;
using Microsoft.Extensions.Options;

namespace HpacSafety.Worker.Summarization;

/// <summary>
///     The one concrete <see cref="ISummarizer" />, an OpenAI-compatible caller: loads the
///     current versioned prompt, applies the deterministic marking pass, makes exactly one
///     <see cref="IAiMediator" /> call with <c>AiChatClient:Model</c> and
///     <c>AiChatClient:ReasoningEffort</c>, and strictly validates the response. It knows no
///     provider: the mediator picks the handler by the model's name (ADR-0104).
/// </summary>
public sealed partial class OpenAiSummarizer : ISummarizer
{
	/// <summary>The current prompt file under <c>Prompts/</c>. Bump on any behavior change.</summary>
	public const string CurrentPromptFileName = "summarize-anonymize.v4.md";

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

	private readonly IAiMediator _mediator;
	private readonly string? _model;
	private readonly ReasoningEffort? _reasoningEffort;
	private readonly string _promptsDirectory;
	private string? _cachedPrompt;

	public OpenAiSummarizer(IAiMediator mediator,
								  IOptions<AiChatClientOptions> options)
	{
		ArgumentNullException.ThrowIfNull(mediator);
		ArgumentNullException.ThrowIfNull(options);

		_mediator = mediator;
		_model = options.Value.Model;
		_reasoningEffort = options.Value.ReasoningEffort;
		_promptsDirectory = Path.Combine(AppContext.BaseDirectory, "Prompts");
	}

	/// <inheritdoc />
	public async Task<SummaryDraft> Summarize(SummarizationInput input,
											  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(input);

		// With a key, startup validation guarantees both; without one, the client
		// would refuse anyway. Either way nothing is sent with an empty model name.
		if (string.IsNullOrWhiteSpace(_model) || _reasoningEffort is not { } reasoningEffort)
		{
			throw new SummarizationFailedException("No summarization model and reasoning level are configured.");
		}

		var marked = PrivateValueMarker.Mark(input);
		var systemPrompt = LoadPrompt();
		var userMessage = BuildUserMessage(marked);

		string response;
		try
		{
			response = await _mediator.Complete(
				new AiChatRequest(
					_model,
					reasoningEffort,
					[new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userMessage)]),
				cancellationToken).ConfigureAwait(false);
		}
		catch (AiMediatorUnavailableException exception)
		{
			throw new SummarizationFailedException("The AI chat provider was unavailable for this summarization attempt.", exception);
		}

		var (textEn, textFr) = ParseStrictResponse(response, marked.ExpectedSections);
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
			[.. input.PrivateContext.Select(ToRequestField)],
			[.. input.ExpectedSections.Select(section => new SummarizationRequestSection(section.QuestionKey, section.LabelEn, section.LabelFr))]);

		return JsonSerializer.Serialize(payload, RequestSerializerOptions);
	}

	private static SummarizationRequestField ToRequestField(SummarizationField field)
	{
		return new SummarizationRequestField(field.QuestionKey, field.Label, field.Value);
	}

	private static (string TextEn, string TextFr) ParseStrictResponse(string response,
																	  IReadOnlyList<SummarizationSection> expectedSections)
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

		// The summary is Markdown with exactly one `## ` heading per expected section,
		// in form order, worded exactly as the label the reporter saw, and no other
		// heading. A mismatch is a failed attempt like any other, so the outbox's
		// retry budget applies (ADR-0180, REQ-AI-037).
		if (!HasExactlyTheHeadings(payload.AiSummaryEn, [.. expectedSections.Select(section => section.LabelEn)])
			|| !HasExactlyTheHeadings(payload.AiSummaryFr, [.. expectedSections.Select(section => section.LabelFr)]))
		{
			throw new SummarizationFailedException(
				"The AI chat provider's summary did not have exactly the expected section headings in form order.");
		}

		return (payload.AiSummaryEn, payload.AiSummaryFr);
	}

	/// <summary>
	///     Whether every heading in <paramref name="markdown" /> — ATX <c>#</c> headings
	///     and underlined ones — is one of the expected <c>## </c> lines, in order, with
	///     its label's exact text. A line in a code fence is not a heading.
	/// </summary>
	internal static bool HasExactlyTheHeadings(string markdown,
											   IReadOnlyList<string> expectedLabels)
	{
		var found = new List<string>();
		var inFence = false;
		string? previous = null;

		foreach (var rawLine in markdown.Split('\n'))
		{
			var line = rawLine.TrimEnd('\r');
			var trimmed = line.Trim();

			if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
			{
				inFence = !inFence;
				previous = null;
				continue;
			}

			if (inFence)
			{
				continue;
			}

			if (AtxHeading().IsMatch(line))
			{
				found.Add(line.TrimEnd());
				previous = null;
				continue;
			}
			else if (previous is { Length: > 0 } && SetextUnderline().IsMatch(line))
			{
				// An underlined heading is never one of the `## ` lines expected.
				found.Add(previous);
				found.Add(line);
			}

			previous = trimmed.Length == 0 ? null : trimmed;
		}

		return found.SequenceEqual(expectedLabels.Select(label => $"## {label}"), StringComparer.Ordinal);
	}

	[GeneratedRegex(@"^ {0,3}#{1,6}(\s|$)")]
	private static partial Regex AtxHeading();

	[GeneratedRegex(@"^ {0,3}(=+|-+)\s*$")]
	private static partial Regex SetextUnderline();

	private sealed record SummarizationRequestField(string QuestionKey, string Label, string Value);

	private sealed record SummarizationRequestSection(string QuestionKey, string LabelEn, string LabelFr);

	private sealed record SummarizationRequestPayload(
		IReadOnlyList<SummarizationRequestField> ReportContent,
		IReadOnlyList<SummarizationRequestField> PrivateContext,
		IReadOnlyList<SummarizationRequestSection> ExpectedSections);

	[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
	private sealed record SummarizationResponsePayload(string? AiSummaryEn, string? AiSummaryFr);
}
