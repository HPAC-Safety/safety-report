namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>One choice on a seeded question, in both locales.</summary>
/// <param name="Code">
/// The invariant code stored against an answer. It never changes — a rename is
/// a translation change, so historical answers keep pointing at the same thing.
/// Where an answer projects onto a typed property, the code is the enum's own
/// code so <c>EnumCode.TryParse</c> resolves it.
/// </param>
/// <param name="LabelEn">The English label, the source wording.</param>
/// <param name="LabelFr">The French label, machine-translated and unreviewed.</param>
public sealed record SeededOption(string Code, string LabelEn, string LabelFr);
