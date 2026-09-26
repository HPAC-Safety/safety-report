namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     The one row of the <c>admin_pending_counts</c> view: how much work is
///     waiting, for the Admin menu's badges (REQ-MOD-084).
/// </summary>
public sealed class AdminPendingCounts
{
	/// <summary>Live reports the Needs action filter would list.</summary>
	public int ReportsNeedingAction { get; private init; }

	/// <summary>Answers waiting for a second language.</summary>
	public int AnswersAwaitingTranslation { get; private init; }

	/// <summary>Type-ahead values on live questions waiting for a Safety Officer or Administrator to review (ADR-0129).</summary>
	public int TypeAheadValuesAwaitingReview { get; private init; }
}
