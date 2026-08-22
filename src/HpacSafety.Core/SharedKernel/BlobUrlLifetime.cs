namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// How long a pre-signed URL may live. There are no public object URLs, ever, so
/// every URL this system issues expires — and the cap lives here rather than in
/// each adapter, because a rule that each implementation re-states is a rule one
/// of them will eventually re-state differently.
/// See docs/data-handling.md and ADR-0026.
/// </summary>
public static class BlobUrlLifetime
{
    /// <summary>
    /// Fifteen minutes: long enough for an administrator to open a photo or for a
    /// browser to finish one upload, short enough that a URL copied out of a
    /// browser's history is worthless by the time anyone reads it.
    /// </summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(15);

    /// <summary>Returns the lifetime, or throws when it is not short-lived.</summary>
    public static TimeSpan Validate(TimeSpan lifetime) =>
        lifetime <= TimeSpan.Zero
            ? throw new DomainRuleViolationException("A pre-signed URL lifetime must be positive.")
            : lifetime > Maximum
                ? throw new DomainRuleViolationException($"A pre-signed URL may not live longer than {Maximum}.")
                : lifetime;
}
