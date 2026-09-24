using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>answers_awaiting_translation</c> view: a live answer
///     marked for machine translation that has no second language yet
///     (ADR-0080, ADR-0112).
/// </summary>
public sealed class AnswerAwaitingTranslation
{
	/// <summary>The answer.</summary>
	public TinyId Id { get; private init; }

	/// <summary>Which question was answered.</summary>
	public string QuestionKey { get; private init; } = string.Empty;

	/// <summary>The words the reporter gave.</summary>
	public string Value { get; private init; } = string.Empty;

	/// <summary>The language they gave them in.</summary>
	public Locale Locale { get; private init; }

	/// <summary>When it was answered, so the queue reads oldest first.</summary>
	public DateTimeOffset AnsweredAt { get; private init; }
}
