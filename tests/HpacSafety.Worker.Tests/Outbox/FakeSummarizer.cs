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
	private readonly Action? _onCall;

	/// <summary>A fixture summarizer that returns a fixed pair.</summary>
	/// <param name="draft">The pair to return.</param>
	/// <param name="onCall">
	///     Runs synchronously just before the pair is returned — lets a test act
	///     as though something happened while the model call was in flight, such
	///     as a concurrent soft deletion (REQ-DOM-007).
	/// </param>
	public FakeSummarizer((string TextEn, string TextFr) draft,
						  Action? onCall = null)
	{
		_draft = draft;
		_onCall = onCall;
	}

	/// <summary>A fixture summarizer that fails, optionally after acting mid-call.</summary>
	/// <param name="failing">Whether every call fails.</param>
	/// <param name="onCall">Runs just before the failure — see the other constructor.</param>
	public FakeSummarizer(bool failing,
						  Action? onCall = null)
	{
		_failing = failing;
		_onCall = onCall;
	}

	public int CallCount { get; private set; }

	public SummarizationInput? LastInput { get; private set; }

	public Task<SummaryDraft> Summarize(SummarizationInput input,
										CancellationToken cancellationToken)
	{
		CallCount++;
		LastInput = input;

		_onCall?.Invoke();

		if (_failing || _draft is null)
		{
			throw new SummarizationFailedException("The fixture summarizer was told to fail.");
		}

		return Task.FromResult(new SummaryDraft(_draft.Value.TextEn, _draft.Value.TextFr, "fixture-model", "fixture-v1"));
	}
}
