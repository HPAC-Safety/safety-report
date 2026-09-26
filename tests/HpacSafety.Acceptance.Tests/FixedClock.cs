namespace HpacSafety.Acceptance.Tests;

/// <summary>A clock that stands still at one instant, for scenarios that name "now" themselves.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
	public override DateTimeOffset GetUtcNow()
	{
		return now;
	}
}
