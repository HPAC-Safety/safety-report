using Microsoft.Extensions.Options;

namespace HpacSafety.Infrastructure.Observability;

/// <summary>
///     Writes one CloudWatch Embedded Metric Format (EMF) JSON line per
///     <see cref="Publish" /> call, to stdout by default — which both the API
///     and the Worker Lambda functions already ship to CloudWatch Logs, with
///     no extra agent and no AWS SDK call (issue #467).
/// </summary>
public sealed class EmbeddedMetricsPublisher : IMetricsPublisher
{
	private readonly string _namespace;
	private readonly TimeProvider _clock;
	private readonly TextWriter _writer;

	public EmbeddedMetricsPublisher(IOptions<MetricsOptions> options, TimeProvider clock)
		: this(options, clock, Console.Out)
	{
	}

	/// <summary>Writes to a caller-supplied writer instead of stdout — used directly only by tests.</summary>
	public EmbeddedMetricsPublisher(IOptions<MetricsOptions> options, TimeProvider clock, TextWriter writer)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(clock);
		ArgumentNullException.ThrowIfNull(writer);

		_namespace = options.Value.Namespace;
		_clock = clock;
		_writer = writer;
	}

	/// <inheritdoc />
	public void Publish(string metricName,
						double value,
						MetricUnit unit,
						IReadOnlyDictionary<string, string>? dimensions = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(metricName);

		var line = EmbeddedMetricFormatWriter.Write(_namespace, _clock.GetUtcNow(), metricName, value, unit, dimensions);

		// One JSON object per line — CloudWatch Logs parses each log event on
		// its own, and a multi-line write would split across two events.
		_writer.WriteLine(line);
	}
}
