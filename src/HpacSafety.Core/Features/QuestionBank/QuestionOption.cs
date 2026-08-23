using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>One immutable bilingual option on one question revision.</summary>
public sealed class QuestionOption
{
#pragma warning disable CS8618 // EF Core sets every mapped property.
    private QuestionOption()
    {
    }
#pragma warning restore CS8618

    private QuestionOption(TinyId questionId, QuestionOptionDefinition definition, int sortOrder)
    {
        Id = TinyId.New();
        QuestionId = questionId;
        Code = QuestionKey.Normalize(definition.Code);
        LabelEn = NotBlank(definition.LabelEn, "English");
        LabelFr = NotBlank(definition.LabelFr, "French");
        SortOrder = sortOrder;
    }

    /// <summary>Fixed yes/no option codes.</summary>
    public static IReadOnlyList<string> YesNoCodes { get; } = ["yes", "no"];

    /// <summary>Option id.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Owning immutable question revision.</summary>
    public TinyId QuestionId { get; private init; }

    /// <summary>Invariant value stored in an answer.</summary>
    public string Code { get; private init; }

    /// <summary>English display label.</summary>
    public string LabelEn { get; private init; }

    /// <summary>French display label.</summary>
    public string LabelFr { get; private init; }

    /// <summary>Order within the question revision.</summary>
    public int SortOrder { get; private init; }

    internal static QuestionOption Create(
        TinyId questionId,
        QuestionOptionDefinition definition,
        int sortOrder) =>
        new(questionId, definition, sortOrder);

    private static string NotBlank(string value, string language) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new DomainRuleViolationException($"A question option needs a {language} label.")
            : value.Trim();
}
