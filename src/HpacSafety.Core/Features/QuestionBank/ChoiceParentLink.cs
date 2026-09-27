namespace HpacSafety.Core.Features.QuestionBank;

/// <summary>
///     One parent choice a dependent question's choice is offered under: the form
///     offers the choice whenever the parent question is answered with this one
///     (ADR-0151). Owned by its <see cref="QuestionChoice" />, outside every
///     revision.
/// </summary>
/// <remarks>
///     A pair has one row for life. Unticking the parent stamps it
///     <see cref="Deleted" />, never erases it, and ticking it again restores the
///     same row, as writing a removed choice again revives it (ADR-0095). Its
///     parent choice never changes: re-pointing a link stamps it and offers the
///     choice under the new parent choice instead.
/// </remarks>
public class ChoiceParentLink
{
	// EF Core materializes an entity by calling this constructor and then
	// setting every mapped property directly. It exists for the ORM only.
	private ChoiceParentLink()
	{
	}

	internal ChoiceParentLink(TinyId choiceId,
							  TinyId parentChoiceId,
							  DateTimeOffset? deleted = null)
	{
		Id = TinyId.New();
		ChoiceId = choiceId;
		ParentChoiceId = parentChoiceId;
		Deleted = deleted;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The dependent question's choice this link belongs to.</summary>
	public TinyId ChoiceId { get; private init; }

	/// <summary>The parent question's choice it is offered under.</summary>
	public TinyId ParentChoiceId { get; private init; }

	/// <summary>When the parent was unticked or the link re-pointed, if it was. A stamped link offers nothing.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Stamps the link. A link already stamped keeps its first stamp.</summary>
	internal void Remove(DateTimeOffset at)
	{
		Deleted ??= at;
	}

	/// <summary>Ticks the parent again: the same row offers the choice once more.</summary>
	internal void Restore()
	{
		Deleted = null;
	}
}
