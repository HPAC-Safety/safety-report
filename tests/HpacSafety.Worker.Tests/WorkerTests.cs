using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace HpacSafety.Worker.Tests;

/// <summary>
/// The Worker has no outbox consumer yet — the claim loop lands with the outbox
/// issue. What it does have is a lifecycle, and these pin it down: it starts, it
/// announces itself, and it stops without hanging. A background service that
/// throws or hangs on start fails silently inside a container, so this is worth
/// asserting before there is anything more interesting to assert.
/// </summary>
public class WorkerTests
{
    /// <summary>
    /// Starts the Worker, waits for execution to actually finish, and returns
    /// the log it produced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>StartAsync</c> dispatches <c>ExecuteAsync</c> rather than running it
    /// inline, and <c>StopAsync</c> cancels the stopping token before awaiting.
    /// Start-then-stop can therefore cancel the dispatched work <em>before the
    /// thread pool ever runs it</em>, so the body never executes and the log is
    /// empty. That is not hypothetical: it failed exactly that way here.
    /// </para>
    /// <para>
    /// <c>ExecuteTask</c> is the public handle on that dispatched work. Awaiting
    /// it is deterministic. Polling the log with a timeout would be a race
    /// dressed as a test, and a flaky observation would also make the coverage
    /// number flaky — which matters now that a ratchet gates on it.
    /// </para>
    /// </remarks>
    private static async Task<FakeLogger<Worker>> RunToCompletionAsync()
    {
        var logger = new FakeLogger<Worker>();
        var worker = new Worker(logger);

        await worker.StartAsync(CancellationToken.None);
        var execution = worker.ExecuteTask.ShouldNotBeNull();
        await execution;
        await worker.StopAsync(CancellationToken.None);

        return logger;
    }

    [Fact]
    public async Task Given_a_worker_When_it_runs_Then_it_logs_that_it_started()
    {
        // Given / When
        var logger = await RunToCompletionAsync();

        // Then
        logger.Collector.Count.ShouldBe(1);
        logger.LatestRecord.Message.ShouldContain("Worker started");
    }

    [Fact]
    public async Task Given_a_worker_When_it_runs_Then_the_start_record_is_informational()
    {
        // Given / When
        var logger = await RunToCompletionAsync();

        // Then
        logger.LatestRecord.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task Given_a_started_worker_When_it_is_stopped_Then_it_completes_without_hanging()
    {
        // Given
        var worker = new Worker(new FakeLogger<Worker>());
        await worker.StartAsync(CancellationToken.None);

        // When
        var stop = worker.StopAsync(CancellationToken.None);
        var finished = await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5)));

        // Then
        finished.ShouldBe(stop);
    }

    [Fact]
    public async Task Given_a_cancelled_token_When_the_worker_is_started_Then_it_does_not_throw()
    {
        // Given
        var worker = new Worker(new FakeLogger<Worker>());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // When
        var start = async () => await worker.StartAsync(cancelled.Token);

        // Then
        await start.ShouldNotThrowAsync();
    }
}
