using System.Text.Json.Serialization;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     The operator requeue tool the NAT-outage runbook needs (issue #467):
///     invoking the Worker Lambda directly with a <c>{"requeue":"poison"}</c>
///     payload clears poison on every currently-poisoned outbox row — an
///     optional <see cref="Request.From" />/<see cref="Request.To" /> window
///     narrows it to rows poisoned in that span, so a single outage's damage
///     can be requeued without touching poison from an unrelated cause.
///     Authorization is whoever can invoke the function (IAM) — there is no
///     sign-in and no admin UI. Only counts and opaque row IDs are ever
///     logged or returned; the payload is never report content
///     (AGENTS.md invariant 8).
/// </summary>
public static partial class PoisonRequeue
{
	/// <summary>The Lambda invocation payload this tool recognizes.</summary>
	public sealed class Request
	{
		/// <summary>The one recognized value is <c>"poison"</c>, case-insensitively.</summary>
		[JsonPropertyName("requeue")]
		public string? Requeue { get; set; }

		/// <summary>Inclusive lower bound on <c>PoisonedAt</c>, if given.</summary>
		[JsonPropertyName("from")]
		public DateTimeOffset? From { get; set; }

		/// <summary>Inclusive upper bound on <c>PoisonedAt</c>, if given.</summary>
		[JsonPropertyName("to")]
		public DateTimeOffset? To { get; set; }
	}

	/// <summary>What the Lambda invocation returns as its response payload.</summary>
	public sealed class Result
	{
		[JsonPropertyName("requeuedCount")]
		public int RequeuedCount { get; init; }

		[JsonPropertyName("requeuedIds")]
		public IReadOnlyList<string> RequeuedIds { get; init; } = [];
	}

	/// <summary>True when <paramref name="request" /> asks for the poison requeue.</summary>
	public static bool IsPoisonRequeue(Request? request)
	{
		return string.Equals(request?.Requeue, "poison", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	///     Clears poison (<see cref="HpacSafety.Core.Features.Outbox.OutboxMessage.Requeue" />)
	///     on every live, currently-poisoned row whose <c>PoisonedAt</c> falls
	///     within the optional <paramref name="from" />/<paramref name="to" />
	///     window, and commits the change. The next Worker invocation — the
	///     nudge, or the one-minute sweep — claims them like any other due
	///     work; this tool does not drain them itself.
	/// </summary>
	public static async Task<Result> Requeue(
		HpacSafetyDbContext database,
		DateTimeOffset now,
		DateTimeOffset? from,
		DateTimeOffset? to,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(database);
		ArgumentNullException.ThrowIfNull(logger);

		var candidates = await database.OutboxMessages
			.Where(message => message.PoisonedAt != null
							  && message.Deleted == null
							  && (from == null || message.PoisonedAt >= from)
							  && (to == null || message.PoisonedAt <= to))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		foreach (var message in candidates)
		{
			message.Requeue(now);
		}

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		var ids = candidates.Select(message => message.Id.ToString()).ToList();

		// This tool is invoked manually, at most a handful of times a year —
		// never a hot path — so joining IDs unconditionally is not the
		// allocation CA1873 usually guards against.
#pragma warning disable CA1873
		LogRequeued(logger, ids.Count, string.Join(',', ids));
#pragma warning restore CA1873

		return new Result { RequeuedCount = ids.Count, RequeuedIds = ids };
	}

	[LoggerMessage(Level = LogLevel.Information, Message = "Requeued {Count} poisoned outbox message(s): {Ids}")]
	private static partial void LogRequeued(ILogger logger, int count, string ids);
}
