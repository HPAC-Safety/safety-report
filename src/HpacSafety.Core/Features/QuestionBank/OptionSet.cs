namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     A named, reusable list of choices — provinces, aerodromes, glider makes —
///     that more than one question can offer.
/// </summary>
/// <remarks>
///     <para>
///         An option set is <b>mutable</b>, and deliberately so: it is the working list
///         an administrator maintains, and a new aerodrome has to be addable without
///         touching every question that offers the list. That is the opposite of
///         <see cref="QuestionRevision" />, which is immutable because a report has to
///         render exactly what it was asked.
///     </para>
///     <para>
///         Those two facts coexist because a revision does not read its options from
///         here at render time. When a revision is created from a set, the set's live
///         items are <b>copied</b> into the revision's own
///         <see cref="QuestionRevisionOption" /> rows, and the revision answers from that
///         snapshot forever. Editing the set changes what the <i>next</i> revision will
///         offer and changes nothing about any revision that already exists. See
///         ADR-0058.
///     </para>
/// </remarks>
public class OptionSet
{
    private readonly List<OptionSetItem> _items = [];

    // EF Core materializes an entity by calling this constructor and then
    // setting every mapped property and backing field directly. It exists for
    // the ORM and for nothing else — domain code still has to go through the
    // factory that follows, so no caller can reach a half-built aggregate.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
    private OptionSet()
    {
    }
#pragma warning restore CS8618

    private OptionSet(string key, string nameEn, string nameFr, DateTimeOffset at)
    {
        Id = TinyId.New();
        Key = QuestionKey.Normalize(key);
        NameEn = NotBlank(nameEn);
        NameFr = NotBlank(nameFr);
        CreatedAt = at;
    }

    /// <summary>Surrogate key.</summary>
    public TinyId Id { get; private init; }

    /// <summary>Stable invariant identity, so a set can be referred to in a seed or an export.</summary>
    public string Key { get; private init; }

    /// <summary>The English name an administrator picks this set by. Never shown to a reporter.</summary>
    public string NameEn { get; private set; }

    /// <summary>The French name an administrator picks this set by. Never shown to a reporter.</summary>
    public string NameFr { get; private set; }

    /// <summary>When this set was created.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>When this set was retired, if it was.</summary>
    public DateTimeOffset? Deleted { get; private set; }

    /// <summary>The live choices, in the order an administrator arranged them.</summary>
    public IReadOnlyList<OptionSetItem> Items =>
        [.. _items.Where(item => item.Deleted is null).OrderBy(item => item.DisplayOrder)];

    /// <summary>Creates an empty set. Items are added one at a time.</summary>
    public static OptionSet Create(string key, string nameEn, string nameFr, DateTimeOffset at)
    {
        return new OptionSet(key, nameEn, nameFr, at);
    }

    /// <summary>Renames the set for administrators. The reporter never sees this text.</summary>
    public void Rename(string nameEn, string nameFr)
    {
        EnsureNotDeleted();

        NameEn = NotBlank(nameEn);
        NameFr = NotBlank(nameFr);
    }

    /// <summary>
    ///     Adds a choice to the end of the list. A code is unique among the live
    ///     items, so re-adding a code that was removed revives that item rather than
    ///     creating a second row claiming the same code.
    /// </summary>
    public OptionSetItem Add(string code, string labelEn, string labelFr)
    {
        EnsureNotDeleted();

        var normalized = QuestionKey.Normalize(code);

        if (_items.Find(item => item.Code == normalized) is { } existing)
        {
            if (existing.Deleted is null) throw new DomainRuleViolationException($"'{Key}' already offers an option coded '{normalized}'.");

            existing.Restore(NextDisplayOrder(), labelEn, labelFr);
            return existing;
        }

        var item = OptionSetItem.Create(Id, normalized, NextDisplayOrder(), labelEn, labelFr);
        _items.Add(item);
        return item;
    }

    /// <summary>
    ///     Records a choice a reporter typed into a type-ahead that the list did
    ///     not already offer — the pilot who flew at a site nobody had written
    ///     down. See ADR-0063.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Three cases, and the difference between them is the whole method:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             The code is already offered — the reporter typed a site that exists, or
    ///             a second pilot typed the same new one. That item is returned unchanged.
    ///             Adding is idempotent, so a busy weekend at a new site produces one row,
    ///             not five, and it never relabels an administrator's wording with a
    ///             reporter's spelling.
    ///         </item>
    ///         <item>
    ///             The code exists but was removed. It is returned
    ///             <b>
    ///                 without being
    ///                 revived
    ///             </b>
    ///             . An administrator removed it on purpose — it was junk, or a
    ///             duplicate — and a reporter typing it again must not undo that. The
    ///             answer still points at a real row; the list simply does not offer it.
    ///         </item>
    ///         <item>
    ///             The code is new. A new item is created, flagged
    ///             <see cref="OptionSetItem.AddedByReporter" />, and offered from now on.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         A reporter types one language and nothing here translates it — the
    ///         submission path calls no translator at all (ADR-0072). The typed words
    ///         stand in for both languages and the item is marked
    ///         <see cref="OptionSetItem.NeedsTranslation" />, so the next reporter is
    ///         offered the choice immediately and an administrator supplies the real
    ///         second wording from the curation screen.
    ///     </para>
    /// </remarks>
    /// <param name="label">The wording the reporter typed.</param>
    /// <returns>The item this choice is now recorded as.</returns>
    public OptionSetItem AddFromReporter(string label)
    {
        EnsureNotDeleted();

        var normalized = QuestionKey.Normalize(label);

        if (_items.Find(item => item.Code == normalized) is { } existing) return existing;

        var item = OptionSetItem.Create(
            Id, normalized, NextDisplayOrder(),
            label, label,
            true, true);

        _items.Add(item);
        return item;
    }

    /// <summary>Relabels one choice in both official languages. Its code never changes.</summary>
    public void Relabel(string code, string labelEn, string labelFr)
    {
        Live(code).Relabel(labelEn, labelFr);
    }

    /// <summary>
    ///     Arranges the live choices into the given order of codes. Every live code
    ///     has to appear exactly once — a partial order would leave the rest in an
    ///     arbitrary position, which is worse than refusing.
    /// </summary>
    public void Arrange(IReadOnlyList<string> codesInOrder)
    {
        ArgumentNullException.ThrowIfNull(codesInOrder);
        EnsureNotDeleted();

        var normalized = codesInOrder.Select(QuestionKey.Normalize).ToList();
        var live = Items.Select(item => item.Code).ToList();

        if (normalized.Count != live.Count || normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Count
                                           || !live.All(code => normalized.Contains(code, StringComparer.Ordinal)))
            throw new DomainRuleViolationException(
                $"An arrangement of '{Key}' has to list every live option exactly once.");

        for (var i = 0; i < normalized.Count; i++) Live(normalized[i]).MoveTo(i);
    }

    /// <summary>
    ///     Removes a choice from future revisions. A soft delete, always: revisions
    ///     that already snapshotted this choice keep their own copy, and answers
    ///     pointing at that copy are untouched.
    /// </summary>
    public void Remove(string code, DateTimeOffset at)
    {
        EnsureNotDeleted();
        Live(code).Delete(at);
    }

    /// <summary>
    ///     Retires the whole set. A soft delete: revisions built from it keep their
    ///     snapshots, and nothing a reporter ever answered changes.
    /// </summary>
    public void Delete(DateTimeOffset at)
    {
        if (Deleted is not null) return;

        Deleted = at;

        foreach (var item in _items.Where(item => item.Deleted is null)) item.Delete(at);
    }

    /// <summary>
    ///     The live items as revision input, so a new revision can snapshot this
    ///     set. Each carries the item it came from, which is how the authoring UI
    ///     can later tell a snapshot apart from a hand-typed option.
    /// </summary>
    public IReadOnlyList<QuestionOptionInput> AsRevisionOptions()
    {
        return [.. Items.Select(item => new QuestionOptionInput(item.Code, item.LabelEn, item.LabelFr, item.Id))];
    }

    private int NextDisplayOrder()
    {
        return _items.Count == 0 ? 0 : _items.Max(item => item.DisplayOrder) + 1;
    }

    private OptionSetItem Live(string code)
    {
        var normalized = QuestionKey.Normalize(code);

        return _items.Find(item => item.Code == normalized && item.Deleted is null)
               ?? throw new DomainRuleViolationException($"'{Key}' has no live option coded '{normalized}'.");
    }

    private void EnsureNotDeleted()
    {
        if (Deleted is not null) throw new DomainRuleViolationException($"'{Key}' was deleted and cannot be changed.");
    }

    private static string NotBlank(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? throw new DomainRuleViolationException("An option set needs a name in both official languages.")
            : name;
    }
}
