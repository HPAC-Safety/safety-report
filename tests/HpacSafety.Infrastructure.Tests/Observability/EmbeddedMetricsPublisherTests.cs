using System.Text.Json;
using HpacSafety.Infrastructure.Observability;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Observability;

/// <summary>
///     <see cref="EmbeddedMetricsPublisher" /> writes one CloudWatch Embedded
///     Metric Format (EMF) JSON line per call — the shape CloudWatch Logs
///     needs to extract a metric with no agent and no AWS SDK call
///     (issue #467).
/// </summary>
public sealed class EmbeddedMetricsPublisherTests
{
	private static readonly DateTimeOffset At = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void GivenAMetricWithNoDimensions_WhenPublished_ThenTheLineNamesTheNamespaceMetricAndValue()
	{
		// Given
		var writer = new StringWriter();
		var publisher = new EmbeddedMetricsPublisher(Options.Create(new MetricsOptions { Namespace = "HpacSafety" }), new FakeClock(At), writer);

		// When
		publisher.Publish("OutboxOldestAgeSeconds", 1, MetricUnit.Count);

		// Then
		using var document = JsonDocument.Parse(writer.ToString().TrimEnd());
		var root = document.RootElement;

		root.GetProperty("OutboxOldestAgeSeconds").GetDouble().ShouldBe(1);

		var aws = root.GetProperty("_aws");
		aws.GetProperty("Timestamp").GetInt64().ShouldBe(At.ToUnixTimeMilliseconds());

		var metricDirective = aws.GetProperty("CloudWatchMetrics")[0];
		metricDirective.GetProperty("Namespace").GetString().ShouldBe("HpacSafety");
		metricDirective.GetProperty("Dimensions")[0].GetArrayLength().ShouldBe(0);

		var metric = metricDirective.GetProperty("Metrics")[0];
		metric.GetProperty("Name").GetString().ShouldBe("OutboxOldestAgeSeconds");
		metric.GetProperty("Unit").GetString().ShouldBe("Count");
	}

	[Fact]
	public void GivenAMetricWithDimensions_WhenPublished_ThenTheLineNamesEachDimensionAsBothAKeyAndAValue()
	{
		// Given — the publisher stays generic even though the application
		// currently emits only one, dimensionless metric (issue #467).
		var writer = new StringWriter();
		var publisher = new EmbeddedMetricsPublisher(Options.Create(new MetricsOptions { Namespace = "HpacSafety" }), new FakeClock(At), writer);

		// When
		publisher.Publish("ExampleMetric", 1, MetricUnit.Count, new Dictionary<string, string> { ["ExampleDimension"] = "ExampleValue" });

		// Then
		using var document = JsonDocument.Parse(writer.ToString().TrimEnd());
		var root = document.RootElement;

		root.GetProperty("ExampleDimension").GetString().ShouldBe("ExampleValue");

		var metricDirective = root.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0];
		var dimensionNames = metricDirective.GetProperty("Dimensions")[0].EnumerateArray().Select(element => element.GetString()).ToList();
		dimensionNames.ShouldBe(["ExampleDimension"]);
	}

	[Fact]
	public void GivenASecondsUnit_WhenPublished_ThenTheLineNamesSeconds()
	{
		// Given
		var writer = new StringWriter();
		var publisher = new EmbeddedMetricsPublisher(Options.Create(new MetricsOptions()), new FakeClock(At), writer);

		// When
		publisher.Publish("OutboxOldestAgeSeconds", 42, MetricUnit.Seconds);

		// Then
		using var document = JsonDocument.Parse(writer.ToString().TrimEnd());
		var metric = document.RootElement.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0].GetProperty("Metrics")[0];
		metric.GetProperty("Unit").GetString().ShouldBe("Seconds");
	}

	[Fact]
	public void GivenTwoPublishCalls_WhenWritten_ThenEachIsItsOwnLine()
	{
		// Given
		var writer = new StringWriter();
		var publisher = new EmbeddedMetricsPublisher(Options.Create(new MetricsOptions()), new FakeClock(At), writer);

		// When
		publisher.Publish("A", 1, MetricUnit.Count);
		publisher.Publish("B", 2, MetricUnit.Count);

		// Then — CloudWatch Logs parses each log event on its own.
		var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
		lines.Length.ShouldBe(2);
		JsonDocument.Parse(lines[0]).RootElement.GetProperty("A").GetDouble().ShouldBe(1);
		JsonDocument.Parse(lines[1]).RootElement.GetProperty("B").GetDouble().ShouldBe(2);
	}

	private sealed class FakeClock(DateTimeOffset now) : TimeProvider
	{
		public override DateTimeOffset GetUtcNow()
		{
			return now;
		}
	}
}
