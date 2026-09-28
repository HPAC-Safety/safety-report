using System.Runtime.CompilerServices;
using HpacSafety.Core.Features.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Infrastructure.Worker;

/// <summary>
///     Nudges the Worker exactly when a save actually queued outbox work — a
///     report submission, a comment, or a review action — never on a save that
///     touched nothing outbox-shaped (ADR-0123). One instance is shared across
///     every <c>DbContext</c> the process creates (it is registered as a
///     singleton, like every EF Core interceptor here), so the "did this save
///     add an <see cref="OutboxMessage" />" flag is kept per-context rather
///     than on this instance.
/// </summary>
public sealed partial class OutboxNudgeInterceptor(IWorkerNudge nudge, ILogger<OutboxNudgeInterceptor> logger)
	: SaveChangesInterceptor
{
	// EF Core marks a saved Added entity Unchanged once SaveChanges completes,
	// so "was an OutboxMessage added" has to be captured before the save
	// commits and read back after. Keyed by DbContext instance rather than
	// held as a field, because interceptors registered through
	// AddInterceptors are shared across every DbContext the process creates.
	private static readonly ConditionalWeakTable<DbContext, StrongBox<bool>> QueuedWork = new();

	/// <inheritdoc />
	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		ArgumentNullException.ThrowIfNull(eventData);

		Capture(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	/// <inheritdoc />
	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(eventData);

		Capture(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	/// <inheritdoc />
	public override async ValueTask<int> SavedChangesAsync(
		SaveChangesCompletedEventData eventData,
		int result,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(eventData);

		await NudgeIfQueued(eventData.Context, cancellationToken).ConfigureAwait(false);
		return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
	{
		ArgumentNullException.ThrowIfNull(eventData);

		// Fire-and-forget on the synchronous path too — nothing in this
		// codebase calls the sync SaveChanges from request-handling code, but
		// an interceptor covers both for correctness.
		_ = NudgeIfQueued(eventData.Context, CancellationToken.None);
		return base.SavedChanges(eventData, result);
	}

	private static void Capture(DbContext? context)
	{
		if (context is null)
		{
			return;
		}

		var queuedWork = context.ChangeTracker.Entries<OutboxMessage>()
			.Any(entry => entry.State == EntityState.Added);

		QueuedWork.AddOrUpdate(context, new StrongBox<bool>(queuedWork));
	}

	private async Task NudgeIfQueued(DbContext? context,
									 CancellationToken cancellationToken)
	{
		if (context is null
			|| !QueuedWork.TryGetValue(context, out var box)
			|| !box.Value)
		{
			return;
		}

		QueuedWork.Remove(context);

		try
		{
			await nudge.NudgeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Belt and braces on top of each IWorkerNudge implementation's own
			// handling: the save that queued the work already committed, and
			// the EventBridge sweep is the delivery guarantee, not this call.
			LogNudgeFailed(logger, exception);
		}
	}

	[LoggerMessage(Level = LogLevel.Warning, Message = "Nudging the Worker after a save failed; the next EventBridge sweep will pick the work up.")]
	private static partial void LogNudgeFailed(ILogger logger, Exception exception);
}
