namespace HpacSafety.Infrastructure.Observability;

/// <summary>The CloudWatch unit an embedded-metric log line reports a value under.</summary>
public enum MetricUnit
{
	/// <summary>A count of occurrences — <c>"Count"</c> in EMF.</summary>
	Count,

	/// <summary>A duration in seconds — <c>"Seconds"</c> in EMF.</summary>
	Seconds,
}

/// <summary>Maps <see cref="MetricUnit" /> to the literal CloudWatch Embedded Metric Format unit string.</summary>
internal static class MetricUnitExtensions
{
	public static string ToEmfUnit(this MetricUnit unit)
	{
		return unit switch
		{
			MetricUnit.Count => "Count",
			MetricUnit.Seconds => "Seconds",
			_ => throw new ArgumentOutOfRangeException(nameof(unit), unit, message: null),
		};
	}
}
