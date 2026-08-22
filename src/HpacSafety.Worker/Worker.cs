using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Worker;

/// <summary>
/// Consumes <c>outbox_messages</c> and runs the anonymization pipeline.
/// Scaffolding only — the claim loop lands with the Phase 1 outbox issue.
/// See <c>src/HpacSafety.Worker/README.md</c>.
/// </summary>
public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker started; no outbox consumer wired up yet.")]
    private static partial void LogStarted(ILogger logger);
}
