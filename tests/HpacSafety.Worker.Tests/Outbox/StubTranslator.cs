using HpacSafety.Core;
using HpacSafety.Worker.Outbox;

namespace HpacSafety.Worker.Tests.Outbox;

/// <summary>
///     A translator that deterministically transforms each input rather than
///     calling a provider, so a test can assert exactly what
///     <see cref="TranslateAnswersProcessor" /> did with what it got back.
/// </summary>
public sealed class StubTranslator : ITranslator
{
	/// <summary>Every call this translator received, for assertion.</summary>
	public List<(IReadOnlyList<string> Texts, Locale Source, Locale Target)> Calls { get; } = [];

	/// <inheritdoc />
	public bool IsConfigured => true;

	/// <inheritdoc />
	public Task<IReadOnlyList<string>> Translate(
		IReadOnlyList<string> texts, Locale source, Locale target, CancellationToken cancellationToken)
	{
		Calls.Add((texts, source, target));

		IReadOnlyList<string> result = texts.Select(text => $"[{target.Code}] {text}").ToList();
		return Task.FromResult(result);
	}
}
