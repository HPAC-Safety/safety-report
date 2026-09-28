using System.Text.Json.Nodes;

namespace HpacSafety.Infrastructure.Observability;

/// <summary>
///     Builds one CloudWatch Embedded Metric Format (EMF) log line: a JSON
///     object with an <c>_aws</c> metadata block naming the namespace,
///     dimensions, and metric, plus the dimension and metric values as
///     top-level properties. CloudWatch Logs recognizes this shape on ingest
///     and publishes the metric with no agent and no CloudWatch API call —
///     see
///     <see href="https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/CloudWatch_Embedded_Metric_Format_Specification.html" />.
/// </summary>
internal static class EmbeddedMetricFormatWriter
{
	public static string Write(string @namespace,
							   DateTimeOffset timestamp,
							   string metricName,
							   double value,
							   MetricUnit unit,
							   IReadOnlyDictionary<string, string>? dimensions)
	{
		var dimensionNames = dimensions is { Count: > 0 } ? dimensions.Keys.ToArray() : [];

		var dimensionNameArray = new JsonArray();
		foreach (var name in dimensionNames)
		{
			dimensionNameArray.Add(name);
		}

		var root = new JsonObject
		{
			["_aws"] = new JsonObject
			{
				["Timestamp"] = timestamp.ToUnixTimeMilliseconds(),
				["CloudWatchMetrics"] = new JsonArray
				{
					new JsonObject
					{
						["Namespace"] = @namespace,
						["Dimensions"] = new JsonArray { dimensionNameArray },
						["Metrics"] = new JsonArray
						{
							new JsonObject
							{
								["Name"] = metricName,
								["Unit"] = unit.ToEmfUnit(),
							},
						},
					},
				},
			},
			[metricName] = value,
		};

		if (dimensions is not null)
		{
			foreach (var (name, dimensionValue) in dimensions)
			{
				root[name] = dimensionValue;
			}
		}

		return root.ToJsonString();
	}
}
