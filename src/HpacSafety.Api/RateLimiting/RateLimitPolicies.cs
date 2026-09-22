namespace HpacSafety.Api.RateLimiting;

/// <summary>The named <c>RateLimiter</c> policies this API registers.</summary>
public static class RateLimitPolicies
{
	/// <summary>Applied to <c>POST /api/v1/reports</c>. Partitioned by trusted client IP.</summary>
	public const string PublicSubmission = "public-submission";

	/// <summary>
	///     Applied to <c>POST /api/auth/token</c>. Partitioned by the attempted
	///     identity, not by IP — the same reporter's IP legitimately submits many
	///     reports, but nobody legitimately attempts many different sign-ins from
	///     one browser in a short window. See issue #15.
	/// </summary>
	public const string SignIn = "sign-in";
}
