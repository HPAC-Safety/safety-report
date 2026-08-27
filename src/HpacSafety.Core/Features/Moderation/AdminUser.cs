using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Moderation;

/// <summary>
/// A row on the authorization allowlist. Credentials are never stored here —
/// authentication happens against members.hpac.ca and this table only answers
/// "and may they".
/// </summary>
public class AdminUser
{
    /// <summary>Adds someone to the allowlist.</summary>
    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // constructor or factory that follows, so no caller can reach a half-built
    // aggregate. See ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private AdminUser()
    {
    }
#pragma warning restore CS8618

    public AdminUser(string memberIdentifier, AdminRole role, DateTimeOffset at)
    {
        Id = TinyId.New();
        MemberIdentifier = memberIdentifier;
        Role = role;
        CreatedAt = at;
        IsActive = true;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Who they are upstream. Never a credential.</summary>
    public string MemberIdentifier { get; private init; }

    /// <summary>What they may do.</summary>
    public AdminRole Role { get; private set; }

    /// <summary>Whether the allowlist still admits them.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When they were added.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When they were removed from the allowlist entirely, if they were.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>True when they may edit the question bank.</summary>
    public bool MayEditQuestions => IsActive && Role == AdminRole.Administrator;

    /// <summary>Changes what they may do.</summary>
    public void ChangeRole(AdminRole role) => Role = role;

    /// <summary>Removes them from the allowlist without deleting their audit trail.</summary>
    public void Revoke() => IsActive = false;
}
