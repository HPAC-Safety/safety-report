namespace HpacSafety.Infrastructure.Observability;

/// <summary>
///     Binds the <c>Metrics</c> configuration section. Both hosts set
///     <c>Metrics__Namespace</c> to the same literal <c>infra/observability.tf</c>'s
///     <c>local.metric_namespace</c> holds, so an alarm and the log line it
///     reads always agree on the namespace without either side hard-coding it
///     twice.
/// </summary>
public sealed class MetricsOptions
{
	public const string SectionName = "Metrics";

	/// <summary>The CloudWatch namespace every embedded-metric log line reports under.</summary>
	public string Namespace { get; set; } = "HpacSafety";
}
