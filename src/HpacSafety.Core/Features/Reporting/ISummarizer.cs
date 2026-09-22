namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     What one model call returned, with the provenance stamped alongside it so any
///     published text traces back to exactly what produced it.
/// </summary>
/// <param name="TextEn">The English summary.</param>
/// <param name="TextFr">The French summary.</param>
/// <param name="Model">The model identifier.</param>
/// <param name="PromptVersion">The versioned prompt from <c>prompts/</c>.</param>
public sealed record SummaryDraft(string TextEn, string TextFr, string Model, string PromptVersion);

/// <summary>
///     Produces one anonymized bilingual summary pair from a single model call.
/// </summary>
/// <remarks>
///     The model receives non-private report content, already marked by
///     <see cref="PrivateValueMarker" />, and private redaction context as distinct
///     sections. Private context may identify information to omit or generalize, but
///     it is never an eligible source of summary facts.
/// </remarks>
public interface ISummarizer
{
	/// <summary>Summarizes partitioned report fields into both languages in one call.</summary>
	/// <exception cref="SummarizationFailedException">
	///     The provider was unreachable/unconfigured, or its response failed strict
	///     validation (not exactly two nonblank strings, extra key, or a Markdown
	///     fence).
	/// </exception>
	Task<SummaryDraft> Summarize(SummarizationInput input, CancellationToken cancellationToken);
}

/// <summary>
///     A summarization attempt failed: the provider was unreachable/unconfigured, or
///     its response did not pass strict validation.
/// </summary>
/// <remarks>The message never contains report content, private context, or the raw response.</remarks>
public sealed class SummarizationFailedException : Exception
{
	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, operator-facing explanation.</param>
	public SummarizationFailedException(string message)
		: base(message)
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, operator-facing explanation.</param>
	/// <param name="innerException">The underlying failure. Never surfaced beyond this message.</param>
	public SummarizationFailedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Creates the exception.</summary>
	public SummarizationFailedException()
		: base("Summarization failed.")
	{
	}
}
