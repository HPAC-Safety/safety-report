using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace HpacSafety.Worker.Tests;

/// <summary>
///     Lifecycle only — no outbox processor is registered, so a claim iteration
///     resolves its scope, finds nothing to drain, and idles. What these pin down
///     is that the Worker starts, announces itself, and stops promptly without
///     hanging. A background service that throws or hangs on start fails silently
///     inside a container, so this is worth asserting before there is anything
///     more interesting to assert. <see cref="Outbox.TranslateAnswersProcessorTests" />
///     and the Infrastructure-level <c>OutboxClaimer</c> tests cover the actual
///     claim/translate behavior against a real database.
/// </summary>
public class WorkerTests
{
	/// <summary>
	///     A working <see cref="IServiceScopeFactory" /> with no
	///     <c>IOutboxMessageProcessor</c> registered, so the claim loop's per-scope
	///     <see cref="HpacSafetyDbContext" /> is constructed but never queried —
	///     no real database connection is required for a lifecycle test.
	/// </summary>
	private static IServiceScopeFactory BuildScopeFactory()
	{
		var services = new ServiceCollection();
		services.AddDbContext<HpacSafetyDbContext>(options =>
			options.UseNpgsql("Host=localhost;Database=hpac_worker_tests_unused"));

		return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
	}

	/// <summary>
	///     Starts the Worker, waits for the started log record to appear, stops it,
	///     and returns the log it produced.
	/// </summary>
	/// <remarks>
	///     The claim loop runs until <c>StopAsync</c> cancels it, so
	///     <c>ExecuteTask</c> no longer completes on its own the way the pre-loop
	///     scaffolding did — this waits for the one observable side effect
	///     (the started log) instead of for the task to finish.
	/// </remarks>
	private static async Task<FakeLogger<Worker>> RunAndStopAsync()
	{
		var logger = new FakeLogger<Worker>();
		var worker = new Worker(BuildScopeFactory(), TimeProvider.System, logger);

		await worker.StartAsync(CancellationToken.None);

		var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
		while (logger.Collector.Count == 0 && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(10));
		}

		await worker.StopAsync(CancellationToken.None);

		return logger;
	}

	[Fact]
	public async Task GivenWorker_WhenRuns_ThenLogsStarted()
	{
		// Given / When
		var logger = await RunAndStopAsync();

		// Then
		logger.Collector.Count.ShouldBeGreaterThanOrEqualTo(1);
		logger.LatestRecord.Message.ShouldContain("Worker started");
	}

	[Fact]
	public async Task GivenWorker_WhenRuns_ThenStartRecordIsInformational()
	{
		// Given / When
		var logger = await RunAndStopAsync();

		// Then
		logger.LatestRecord.Level.ShouldBe(LogLevel.Information);
	}

	[Fact]
	public async Task GivenStartedWorker_WhenStopped_ThenCompletesWithoutHanging()
	{
		// Given
		var worker = new Worker(BuildScopeFactory(), TimeProvider.System, new FakeLogger<Worker>());
		await worker.StartAsync(CancellationToken.None);

		// When
		var stop = worker.StopAsync(CancellationToken.None);
		var finished = await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5)));

		// Then
		finished.ShouldBe(stop);
	}

	[Fact]
	public async Task GivenCancelledToken_WhenWorkerIsStarted_ThenDoesNotThrow()
	{
		// Given
		var worker = new Worker(BuildScopeFactory(), TimeProvider.System, new FakeLogger<Worker>());
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		// When
		var start = async () => await worker.StartAsync(cancelled.Token);

		// Then
		await start.ShouldNotThrowAsync();
	}
}
