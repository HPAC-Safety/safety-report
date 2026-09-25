namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>One choice on a seeded question, in both locales.</summary>
/// <param name="Code">
///     The choice's invariant code, which a conditional question names
///     (ADR-0074). It never changes. An answer names the choice itself, not the
///     code (ADR-0128).
/// </param>
/// <param name="LabelEn">The English label, the source wording.</param>
/// <param name="LabelFr">The French label, machine-translated and unreviewed.</param>
public sealed record SeededOption(string Code, string LabelEn, string LabelFr);
