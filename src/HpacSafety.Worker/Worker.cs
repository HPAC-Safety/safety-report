using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Worker.Outbox;

namespace HpacSafety.Worker;

/// <summary>
///     Consumes <c>outbox_messages</c>. Each registered <see cref="IOutboxMessageProcessor" />
///     drains its own message type in turn, one claim at a time, so adding a new
///     kind of outbox work (summarization, attachment processing) means
///     registering another processor rather than changing this loop.
/// </summary>
/// <remarks>
///     A processor is scoped — it holds the same <see cref="HpacSafetyDbContext" />
///     the claim transaction uses, so its writes commit atomically with the claim.
///     This is a singleton <see cref="BackgroundService" />, so processors are
///     resolved fresh from a new scope every iteration rather than injected into
///     the constructor, which would be a scoped-from-singleton DI error.
///
///     Deployed, the Worker runs as a Lambda function that drains once per
///     invocation and returns instead of polling (ADR-0123); this loop only runs
///     in a host with no Lambda runtime — developer machines and CI, via
///     <c>docker-compose.yml</c>. Both hosts share the same claim pass,
///     <see cref="Outbox.OutboxDrainPass" />, so a processor behaves identically
///     either way.
/// </remarks>
public sealed partial class Worker(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<Worker> logger)
	: BackgroundService
{
	private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(5);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		LogStarted(logger);

		while (!stoppingToken.IsCancellationRequested)
		{
			var claimedAny = await OutboxDrainPass.RunOnce(scopeFactory, clock, stoppingToken).ConfigureAwait(false);

			if (!claimedAny)
			{
				try
				{
					await Task.Delay(IdlePollInterval, clock, stoppingToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}
	}

	[LoggerMessage(Level = LogLevel.Information, Message = "Worker started.")]
	private static partial void LogStarted(ILogger logger);
}
