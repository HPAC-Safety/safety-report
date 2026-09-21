using HpacSafety.Core;
using HpacSafety.Core.Features.QuestionBank;

namespace HpacSafety.Api.Admin;

/// <summary>
/// One question as the authoring screen needs it: its stable identity plus
/// every field of its current revision, flattened.
/// </summary>
/// <remarks>
/// Purpose-built for this one screen, per <c>skills/persist-hpac-data</c>. It
/// carries no answer, no report, and no reporter data of any kind — the
/// question bank is form definition, not report content.
/// </remarks>
public sealed record QuestionView(
    string Id,
    string Key,
    string RevisionId,
    int RevisionNumber,
    string Type,
    bool IsSystem,
    bool IsRequired,
    bool IsPrivate,
    bool IsActive,
    int DisplayOrder,
    string? SectionKey,
    string? DependsOnQuestionId,
    string? OptionSetId,
    string LabelEn,
    string LabelFr,
    string? HelpTextEn,
    string? HelpTextFr,
    string? PlaceholderEn,
    string? PlaceholderFr,
    IReadOnlyList<OptionView> Options)
{
    /// <summary>Flattens a question and its current revision for the screen.</summary>
    public static QuestionView Of(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var revision = question.CurrentRevision;

        return new QuestionView(
            question.Id.Value,
            question.Key,
            revision.Id.Value,
            revision.RevisionNumber,
            EnumCode.Of(revision.Type),
            question.IsSystem,
            revision.IsRequired,
            revision.IsPrivate,
            revision.IsActive,
            revision.DisplayOrder,
            revision.SectionKey,
            revision.DependsOnQuestionId?.Value,
            revision.OptionSetId?.Value,
            revision.LabelEn,
            revision.LabelFr,
            revision.HelpTextEn,
            revision.HelpTextFr,
            revision.PlaceholderEn,
            revision.PlaceholderFr,
            [.. revision.Options
                .OrderBy(option => option.DisplayOrder)
                .Select(option => new OptionView(option.Code, option.LabelEn, option.LabelFr, option.SourceItemId?.Value))]);
    }
}

/// <summary>One choice on a question revision, in both official languages.</summary>
public sealed record OptionView(string Code, string LabelEn, string LabelFr, string? SourceItemId);

/// <summary>
/// What an administrator submits to create a question or to save an edit. An
/// edit produces a new revision; nothing here patches a row that exists.
/// </summary>
public sealed record SaveQuestionRequest(
    string? Key,
    string Type,
    string LabelEn,
    string LabelFr,
    string? HelpTextEn,
    string? HelpTextFr,
    string? PlaceholderEn,
    string? PlaceholderFr,
    bool IsRequired,
    bool IsPrivate,
    bool IsActive,
    string? SectionKey,
    string? DependsOnQuestionId,
    string? OptionSetId,
    IReadOnlyList<OptionInput>? Options);

/// <summary>One option as authored. A code is normalized server-side.</summary>
public sealed record OptionInput(string Code, string LabelEn, string LabelFr);

/// <summary>Every question in the order the administrator arranged them.</summary>
public sealed record ReorderQuestionsRequest(IReadOnlyList<string> QuestionIdsInOrder);

/// <summary>A reusable choice list as the authoring screen needs it.</summary>
public sealed record OptionSetView(
    string Id,
    string Key,
    string NameEn,
    string NameFr,
    IReadOnlyList<OptionView> Items)
{
    /// <summary>Flattens a set and its live items.</summary>
    public static OptionSetView Of(OptionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new OptionSetView(
            set.Id.Value,
            set.Key,
            set.NameEn,
            set.NameFr,
            [.. set.Items.Select(item => new OptionView(item.Code, item.LabelEn, item.LabelFr, item.Id.Value))]);
    }
}

/// <summary>What an administrator submits to create or replace a choice list.</summary>
public sealed record SaveOptionSetRequest(
    string? Key,
    string NameEn,
    string NameFr,
    IReadOnlyList<OptionInput> Items);
