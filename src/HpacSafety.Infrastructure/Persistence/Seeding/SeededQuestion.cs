using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Infrastructure.Persistence.Seeding;

/// <summary>One complete bilingual revision in the initial question bank.</summary>
public sealed record SeededQuestion(
    string Key,
    QuestionType Type,
    bool IsPrivate,
    bool IsActive,
    string? SectionKey,
    string LabelEn,
    string LabelFr,
    string? HelpEn,
    string? HelpFr,
    IReadOnlyList<SeededOption> Options);
