namespace HpacSafety.Infrastructure.Observability;

/// <summary>
///     Publishes one operational metric value. The implementation this
///     codebase runs (<see cref="EmbeddedMetricsPublisher" />) writes a
///     CloudWatch Embedded Metric Format (EMF) log line — no AWS SDK call, no
///     new dependency. CloudWatch Logs recognizes the shape on ingest and
///     extracts the metric itself (issue #467).
/// </summary>
public interface IMetricsPublisher
{
	/// <summary>
	///     Publishes one data point. Every dimension must be a small,
	///     code-defined value — an outbox message type, an error category —
	///     never anything derived from report content (AGENTS.md invariant 8).
	/// </summary>
	/// <param name="metricName">
	///     The metric's name, exactly as the matching
	///     <c>aws_cloudwatch_metric_alarm</c> in <c>infra/observability.tf</c>
	///     names it.
	/// </param>
	/// <param name="value">The value observed.</param>
	/// <param name="unit">The unit <paramref name="value" /> is in.</param>
	/// <param name="dimensions">
	///     Optional dimension name/value pairs. Every call for the same
	///     <paramref name="metricName" /> should use the same dimension
	///     <em>names</em> — CloudWatch treats a differently-dimensioned point
	///     as a distinct metric stream.
	/// </param>
	void Publish(string metricName,
				double value,
				MetricUnit unit,
				IReadOnlyDictionary<string, string>? dimensions = null);
}
