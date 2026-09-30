namespace HpacSafety.Core.Features.Reporting;

/// <summary>A labeled answer prepared for the summarization model.</summary>
/// <param name="QuestionKey">The question's stable key.</param>
/// <param name="Label">The question's wording in the report's language.</param>
/// <param name="Value">The answer as the model reads it: <c>true</c> or <c>false</c> for a yes/no (ADR-0130).</param>
/// <param name="IsBoolean">
///     Whether the answer is a yes/no or checkbox. The marking pass never uses one as
///     a candidate, or a private yes/no would mark every literal <c>true</c> in the
///     narrative (ADR-0130).
/// </param>
public sealed record SummarizationField(string QuestionKey, string Label, string Value, bool IsBoolean = false);

/// <summary>
///     An answer paired with the immutable privacy classification copied from its
///     question when the report was submitted.
/// </summary>
public sealed record ClassifiedReportField(SummarizationField Field, bool IsPrivate);

/// <summary>
///     One section the summary must contain: a public paragraph question on the
///     report, with its wording in both languages from the revision the reporter
///     answered, the trailing colon already absent (ADR-0180).
/// </summary>
/// <param name="QuestionKey">The question's stable key.</param>
/// <param name="LabelEn">The English label: the exact text of the English heading.</param>
/// <param name="LabelFr">The French label: the exact text of the French heading.</param>
public sealed record SummarizationSection(string QuestionKey, string LabelEn, string LabelFr);

/// <summary>
///     The only input shape accepted by a summarizer. Report content supplies facts
///     for the summary; private context supplies redaction hints and must not be
///     restated as facts.
/// </summary>
public sealed class SummarizationInput
{
	private SummarizationInput(
		IReadOnlyList<SummarizationField> reportContent,
		IReadOnlyList<SummarizationField> privateContext,
		IReadOnlyList<SummarizationSection> expectedSections)
	{
		ReportContent = reportContent;
		PrivateContext = privateContext;
		ExpectedSections = expectedSections;
	}

	/// <summary>
	///     The sections the summary must have, in form order: one per public paragraph
	///     question on the report, blank ones included. Empty when the report has none,
	///     in which case the summary has no headings.
	/// </summary>
	public IReadOnlyList<SummarizationSection> ExpectedSections { get; }

	/// <summary>Non-private fields eligible to contribute facts.</summary>
	public IReadOnlyList<SummarizationField> ReportContent { get; }

	/// <summary>Private values the model may use only to recognize and remove identifiers.</summary>
	public IReadOnlyList<SummarizationField> PrivateContext { get; }

	/// <summary>Partitions fields so callers cannot mix private values into report content.</summary>
	public static SummarizationInput Partition(IEnumerable<ClassifiedReportField> fields,
											   IEnumerable<SummarizationSection>? expectedSections = null)
	{
		ArgumentNullException.ThrowIfNull(fields);

		var reportContent = new List<SummarizationField>();
		var privateContext = new List<SummarizationField>();

		foreach (var classified in fields)
		{
			ArgumentNullException.ThrowIfNull(classified);
			ArgumentNullException.ThrowIfNull(classified.Field);
			(classified.IsPrivate ? privateContext : reportContent).Add(classified.Field);
		}

		return new SummarizationInput(reportContent.AsReadOnly(), privateContext.AsReadOnly(), (expectedSections ?? []).ToList().AsReadOnly());
	}

	/// <summary>
	///     Rebuilds an input with report content replaced, keeping the same private
	///     context. Used only by <see cref="PrivateValueMarker" /> to apply its
	///     deterministic marking pass without letting other callers construct an
	///     input that mixes the two sections arbitrarily.
	/// </summary>
	internal static SummarizationInput WithReportContent(
		SummarizationInput input,
		IReadOnlyList<SummarizationField> reportContent)
	{
		ArgumentNullException.ThrowIfNull(input);
		ArgumentNullException.ThrowIfNull(reportContent);

		return new SummarizationInput(reportContent, input.PrivateContext, input.ExpectedSections);
	}
}
