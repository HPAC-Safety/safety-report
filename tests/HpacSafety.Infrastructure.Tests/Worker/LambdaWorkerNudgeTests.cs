using HpacSafety.Infrastructure.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace HpacSafety.Infrastructure.Tests.Worker;

/// <summary>
///     <see cref="LambdaWorkerNudge" />'s own decision — invoke, and swallow
///     and log any failure rather than let it fail the caller — proven
///     against a fake <see cref="ILambdaInvoker" /> (ADR-0123).
/// </summary>
public sealed class LambdaWorkerNudgeTests
{
	private const string FunctionName = "hpac-safety-worker";

	[Fact]
	public async Task GivenTheInvokerSucceeds_WhenNudged_ThenItInvokesTheNamedFunction()
	{
		// Given
		var invoker = new RecordingInvoker();
		var nudge = new LambdaWorkerNudge(invoker, FunctionName, new FakeLogger<LambdaWorkerNudge>());

		// When
		await nudge.NudgeAsync(CancellationToken.None);

		// Then
		invoker.InvokedFunctionNames.ShouldBe([FunctionName]);
	}

	[Fact]
	public async Task GivenTheInvokerThrows_WhenNudged_ThenTheFailureIsSwallowed()
	{
		// Given
		var invoker = new ThrowingInvoker(new InvalidOperationException("Synthetic Lambda invoke failure."));
		var logger = new FakeLogger<LambdaWorkerNudge>();
		var nudge = new LambdaWorkerNudge(invoker, FunctionName, logger);

		// When
		var nudging = async () => await nudge.NudgeAsync(CancellationToken.None);

		// Then — the EventBridge sweep is the delivery guarantee; a nudge
		// never fails the request it rode in on.
		await nudging.ShouldNotThrowAsync();
		logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Warning);
	}

	[Fact]
	public async Task GivenTheInvokerThrowsOperationCanceled_WhenNudged_ThenItPropagates()
	{
		// Given — cancellation is not a nudge failure to swallow and log; it is
		// the caller's own cooperative cancellation.
		var invoker = new ThrowingInvoker(new OperationCanceledException());
		var nudge = new LambdaWorkerNudge(invoker, FunctionName, new FakeLogger<LambdaWorkerNudge>());

		// When
		var nudging = async () => await nudge.NudgeAsync(CancellationToken.None);

		// Then
		await nudging.ShouldThrowAsync<OperationCanceledException>();
	}

	private sealed class RecordingInvoker : ILambdaInvoker
	{
		public List<string> InvokedFunctionNames { get; } = [];

		public Task InvokeAsync(string functionName,
								CancellationToken cancellationToken)
		{
			InvokedFunctionNames.Add(functionName);
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingInvoker(Exception exception) : ILambdaInvoker
	{
		public Task InvokeAsync(string functionName,
								CancellationToken cancellationToken)
		{
			throw exception;
		}
	}
}
