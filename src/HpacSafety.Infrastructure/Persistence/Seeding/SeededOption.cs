namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>One choice on a seeded question, in both locales.</summary>
/// <param name="Code">
/// The invariant code stored against an answer. Changing a code creates a new
/// complete question revision, so historical answers retain the value shown.
/// </param>
/// <param name="LabelEn">The English label, the source wording.</param>
/// <param name="LabelFr">The reviewed French label.</param>
public sealed record SeededOption(string Code, string LabelEn, string LabelFr);
