namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// Thrown when an operation would break a domain rule — publishing a report
/// nobody consented to publish or creating an incomplete question revision.
/// </summary>
public class DomainRuleViolationException : Exception
{
    /// <summary>Creates the exception with a message describing the rule.</summary>
    public DomainRuleViolationException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public DomainRuleViolationException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Creates the exception with no message.</summary>
    public DomainRuleViolationException() { }
}
