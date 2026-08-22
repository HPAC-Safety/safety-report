namespace HpacSafety.Core.Administration;

/// <summary>
/// A row on the authorization allowlist. Credentials are never stored here —
/// authentication happens against members.hpac.ca and this table only answers
/// "and may they".
/// </summary>
public class AdminUser
{
    /// <summary>Adds someone to the allowlist.</summary>
    public AdminUser(string memberIdentifier, AdminRole role, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        MemberIdentifier = memberIdentifier;
        Role = role;
        CreatedAt = at;
        IsActive = true;
    }

    /// <summary>Surrogate key.</summary>
    public Guid Id { get; private init; }

    /// <summary>Who they are upstream. Never a credential.</summary>
    public string MemberIdentifier { get; private init; }

    /// <summary>What they may do.</summary>
    public AdminRole Role { get; private set; }

    /// <summary>Whether the allowlist still admits them.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When they were added.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>True when they may edit the question bank.</summary>
    public bool MayEditQuestions => IsActive && Role == AdminRole.Administrator;

    /// <summary>Changes what they may do.</summary>
    public void ChangeRole(AdminRole role) => Role = role;

    /// <summary>Removes them from the allowlist without deleting their audit trail.</summary>
    public void Revoke() => IsActive = false;
}
