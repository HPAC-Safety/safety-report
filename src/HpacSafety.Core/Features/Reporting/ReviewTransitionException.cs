namespace HpacSafety.Core.Features.Reporting;

/// <summary>
///     A review action was asked of a report whose status does not allow it
///     (REQ-DOM-014). Nothing changed. The message names the action and the status
///     only — never report content — so it is safe in a problem response.
/// </summary>
public sealed class ReviewTransitionException : DomainRuleViolationException
{
	/// <summary>Creates the exception for an action the report's status refuses.</summary>
	/// <param name="action">What was attempted, such as "approve the pair".</param>
	/// <param name="status">The status the report is in.</param>
	public ReviewTransitionException(string action,
									 ReportStatus status)
		: base($"A report that is {EnumCode.Of(status).Replace('_', ' ')} cannot {action}.")
	{
	}

	/// <summary>Creates the exception.</summary>
	public ReviewTransitionException()
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe explanation.</param>
	public ReviewTransitionException(string message)
		: base(message)
	{
	}

	/// <summary>Creates the exception.</summary>
	/// <param name="message">A safe explanation.</param>
	/// <param name="innerException">The underlying failure.</param>
	public ReviewTransitionException(string message,
									 Exception innerException)
		: base(message, innerException)
	{
	}
}
