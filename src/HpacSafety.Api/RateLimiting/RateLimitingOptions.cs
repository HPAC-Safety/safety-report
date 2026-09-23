namespace HpacSafety.Api.RateLimiting;

/// <summary>
///     One sliding-window policy's shape. Bound twice from configuration, once
///     per policy — public submission and sign-in deliberately use the same
///     shape with different (stricter) sign-in numbers, rather than two
///     different option types for what is the same three knobs. See issue #15.
/// </summary>
public sealed class SlidingWindowPolicyOptions
{
	/// <summary>How many requests a partition may make within one window.</summary>
	public int PermitLimit { get; set; }

	/// <summary>The window's length, in seconds.</summary>
	public int WindowSeconds { get; set; }

	/// <summary>
	///     How many further requests queue, rather than reject immediately, once
	///     the window's permits are exhausted. Zero means reject immediately —
	///     the right choice for both policies here: queuing a blocked sign-in or
	///     submission would only delay the same rejection.
	/// </summary>
	public int QueueLimit { get; set; }
}

/// <summary>
///     The two rate-limiting policies this API applies: one to public report
///     submission (partitioned by trusted client IP), one to sign-in
///     (partitioned by the attempted identity). See issue #15.
/// </summary>
public sealed class RateLimitingOptions
{
	/// <summary>The public-submission policy.</summary>
	public SlidingWindowPolicyOptions PublicSubmission { get; set; } = new();

	/// <summary>The attachment-upload policy, by trusted client IP (ADR-0096).</summary>
	public SlidingWindowPolicyOptions AttachmentUpload { get; set; } = new();

	/// <summary>The sign-in policy — stricter, per issue #15's ruling.</summary>
	public SlidingWindowPolicyOptions SignIn { get; set; } = new();
}
