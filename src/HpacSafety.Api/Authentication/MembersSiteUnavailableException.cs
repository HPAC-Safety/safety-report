namespace HpacSafety.Api.Authentication;

/// <summary>
///     The live members site could not be reached, timed out, or answered with
///     something <see cref="MembersSiteCredentialSource" /> did not expect —
///     distinct from bad credentials, which is a plain verification failure, not
///     an exception. See ADR-0079.
/// </summary>
/// <remarks>
///     The message is safe to show a developer. It never contains the password
///     that was being verified, and never the site's response body — an
///     exception is not a place to put content.
/// </remarks>
public sealed class MembersSiteUnavailableException : Exception
{
	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, developer-facing explanation.</param>
	public MembersSiteUnavailableException(string message)
		: base(message)
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe, developer-facing explanation.</param>
	/// <param name="innerException">
	///     The underlying failure. Never surfaced to a caller — the API reports
	///     <see cref="Exception.Message" /> only.
	/// </param>
	public MembersSiteUnavailableException(string message,
										   Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>Creates the exception.</summary>
	public MembersSiteUnavailableException()
		: base("The members site could not be reached.")
	{
	}
}
