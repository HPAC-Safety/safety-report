using HpacSafety.Core.Features.Reporting;
using HpacSafety.Worker.Outbox;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     A deterministic, controlled <see cref="ISummarizer" /> double — issue #20's
///     "deterministic controlled provider fixtures for tests" — so a test can
///     assert exactly what <see cref="SummarizeReportProcessor" /> sent and did
///     with what came back.
/// </summary>
public sealed class FakeSummarizer : ISummarizer
{
	private readonly (string TextEn, string TextFr)? _draft;
	private readonly bool _failing;

	public FakeSummarizer((string TextEn, string TextFr) draft)
	{
		_draft = draft;
	}

	public FakeSummarizer(bool failing)
	{
		_failing = failing;
	}

	public int CallCount { get; private set; }

	public SummarizationInput? LastInput { get; private set; }

	public Task<SummaryDraft> Summarize(SummarizationInput input, CancellationToken cancellationToken)
	{
		CallCount++;
		LastInput = input;

		if (_failing || _draft is null)
		{
			throw new SummarizationFailedException("The fixture summarizer was told to fail.");
		}

		return Task.FromResult(new SummaryDraft(_draft.Value.TextEn, _draft.Value.TextFr, "fixture-model", "fixture-v1"));
	}
}
