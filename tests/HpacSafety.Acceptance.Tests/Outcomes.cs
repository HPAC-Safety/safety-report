using System.Net;

namespace HpacSafety.Acceptance.Tests;

/// <summary>
///     The glossary's outcome phrases (.spec/glossary.md, "Outcome phrases") and
///     the HTTP status each one stands for. A scenario says the outcome; the step
///     definition asserts the status (CONV-004).
/// </summary>
internal static class Outcomes
{
	/// <summary>Every outcome phrase, as a regular-expression alternation for a binding.</summary>
	public const string Pattern =
		"created|accepted|refused as unauthenticated|refused as forbidden|not found|refused as invalid|refused as out of date|refused as too frequent";

	/// <summary>The status an outcome phrase stands for.</summary>
	public static HttpStatusCode Status(string outcome)
	{
		return outcome switch
		{
			"created" => HttpStatusCode.Created,
			"accepted" => HttpStatusCode.Accepted,
			"refused as unauthenticated" => HttpStatusCode.Unauthorized,
			"refused as forbidden" => HttpStatusCode.Forbidden,
			"not found" => HttpStatusCode.NotFound,
			"refused as invalid" => HttpStatusCode.BadRequest,
			"refused as out of date" => HttpStatusCode.Conflict,
			"refused as too frequent" => HttpStatusCode.TooManyRequests,
			_ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not one of the glossary's outcome phrases."),
		};
	}
}
