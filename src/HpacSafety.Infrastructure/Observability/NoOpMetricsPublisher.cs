namespace HpacSafety.Infrastructure.Observability;

/// <summary>
///     Discards every metric. Used by tests that construct a processor
///     directly and have nothing to assert about its metrics — the same role
///     <c>NoOpWorkerNudge</c> plays for the nudge port.
/// </summary>
public sealed class NoOpMetricsPublisher : IMetricsPublisher
{
	/// <inheritdoc />
	public void Publish(string metricName,
						double value,
						MetricUnit unit,
						IReadOnlyDictionary<string, string>? dimensions = null)
	{
	}
}
