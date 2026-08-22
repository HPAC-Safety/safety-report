namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// The three tiers in docs/data-handling.md. A field's tier is a property of the
/// field, not of the screen it appears on, and the default is the strictest one:
/// a question added tomorrow is Restricted until someone decides otherwise.
/// </summary>
public enum SensitivityTier
{
    Restricted = 0,
    Internal = 1,
    Publishable = 2,
}
