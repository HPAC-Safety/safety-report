using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>
/// The question bank a clean database seeds. Empty for now — the previous
/// transcription of <c>docs/form-spec.md</c> was retired along with the
/// <c>Statement</c>/<c>Group</c> question types and the <c>SectionKey</c>
/// field it depended on. A new, correct question set lands in a follow-up
/// change.
/// </summary>
public static class QuestionBankSeed
{
    /// <summary>
    /// The instant every seeded row is stamped with. Fixed, so that applying
    /// the migration twice on two databases produces identical rows.
    /// </summary>
    public static readonly DateTimeOffset SeededAt = new(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The seeded form, in display order.</summary>
    public static IReadOnlyList<SeededQuestion> Questions { get; } = [];
}
