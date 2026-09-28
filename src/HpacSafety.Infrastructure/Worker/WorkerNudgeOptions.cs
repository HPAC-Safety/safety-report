namespace HpacSafety.Infrastructure.Worker;

/// <summary>Binds <c>HpacSafety:Worker:Nudge</c>. See <see cref="WorkerNudgeServiceCollectionExtensions" />.</summary>
public sealed class WorkerNudgeOptions
{
	/// <summary>The configuration section this binds.</summary>
	public const string SectionName = "HpacSafety:Worker:Nudge";

	/// <summary>
	///     The Worker Lambda function's name or ARN. Left unset — Development,
	///     and every test host — <see cref="NoOpWorkerNudge" /> is registered
	///     instead of an AWS Lambda client.
	/// </summary>
	public string? FunctionName { get; set; }
}
