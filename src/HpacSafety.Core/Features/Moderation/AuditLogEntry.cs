using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Moderation;

/// <summary>
/// One moderation action, with who and when. Records identifiers, never report
/// content — see docs/data-handling.md.
/// </summary>
public class AuditLogEntry
{
    /// <summary>Records an action against a target.</summary>
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private AuditLogEntry()
    {
    }
#pragma warning restore CS8618

    public AuditLogEntry(Guid adminUserId, AuditAction action, string targetType, Guid targetId, DateTimeOffset at, string? detail = null)
    {
        Id = Guid.NewGuid();
        AdminUserId = adminUserId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        OccurredAt = at;
        Detail = detail;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>Who acted.</summary>
    public Guid AdminUserId { get; private init; }

    /// <summary>What they did.</summary>
    public AuditAction Action { get; private init; }

    /// <summary>What kind of thing they did it to.</summary>
    public string TargetType { get; private init; }

    /// <summary>Which one.</summary>
    public Guid TargetId { get; private init; }

    /// <summary>When.</summary>
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>Anything else worth keeping. Identifiers, never report content.</summary>
    public string? Detail { get; private init; }
}
