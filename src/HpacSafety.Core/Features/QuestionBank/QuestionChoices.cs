namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     Which choices a question revision actually offers, which is not always the
///     choices it stored.
/// </summary>
/// <remarks>
///     <para>
///         Every option-bearing revision snapshots its choices when it is created, and
///         that snapshot is the permanent record of what a reporter was offered
///         (ADR-0058). For almost every type it is also what to render: a pick-one
///         list is a closed set an administrator curated, and quietly showing a choice
///         the revision never mentioned would make the record a lie.
///     </para>
///     <para>
///         <see cref="QuestionType.Autocomplete" /> is the exception, because it is the
///         one type a reporter can add to. A pilot who flies at a site nobody has
///         written down types it, and the next pilot has to see it — a list that only
///         grows when an administrator publishes a new revision does not solve the
///         problem the type exists for. So an autocomplete backed by a shared set
///         renders the <b>live</b> set, while its snapshot still records what that
///         reporter was shown. The two answer different questions: "what do we offer
///         now" and "what were you offered". See ADR-0063.
///     </para>
///     <para>
///         This is a pure function over rows the caller has already loaded. It does no
///         I/O and it is the one place the rule is written down.
///     </para>
/// </remarks>
public static class QuestionChoices
{
    /// <summary>
    ///     The choices to render for a revision.
    /// </summary>
    /// <param name="revision">The revision being shown.</param>
    /// <param name="optionSet">
    ///     The shared set the revision names, when it names one and the caller has
    ///     loaded it. Null for a revision with hand-typed options, or when the set
    ///     has since been retired.
    /// </param>
    /// <returns>
    ///     The live set's items for an autocomplete backed by a set; the
    ///     revision's own frozen snapshot otherwise.
    /// </returns>
    public static IReadOnlyList<QuestionOptionInput> For(QuestionRevision revision, OptionSet? optionSet)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return optionSet is not null && RendersLiveSet(revision, optionSet)
            ? optionSet.AsRevisionOptions()
            : Snapshot(revision);
    }

    /// <summary>
    ///     Whether this revision renders the live set rather than its snapshot.
    ///     False whenever the set is missing — a retired set leaves the revision
    ///     showing exactly what it recorded, rather than nothing at all.
    /// </summary>
    public static bool RendersLiveSet(QuestionRevision revision, OptionSet? optionSet)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return revision.Type == QuestionType.Autocomplete
               && optionSet is { Deleted: null }
               && revision.OptionSetId == optionSet.Id;
    }

    /// <summary>The revision's own recorded choices, in order.</summary>
    public static IReadOnlyList<QuestionOptionInput> Snapshot(QuestionRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return
        [
            .. revision.Options
                .OrderBy(option => option.DisplayOrder)
                .Select(option => new QuestionOptionInput(
                    option.Code, option.LabelEn, option.LabelFr, option.SourceItemId))
        ];
    }
}
