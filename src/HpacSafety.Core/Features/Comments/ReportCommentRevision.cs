using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Core.Features.Comments;

/// <summary>
///     One version of a comment's text, in the language it was written in.
///     Immutable, except that the Worker fills in its machine translation once
///     (ADR-0114).
/// </summary>
public class ReportCommentRevision
{
	// For EF Core only; see ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private ReportCommentRevision()
	{
	}
#pragma warning restore CS8618

	private ReportCommentRevision(TinyId commentId,
								  int number,
								  string text,
								  Locale locale,
								  DateTimeOffset at)
	{
		Id = TinyId.New();
		CommentId = commentId;
		Number = number;
		Text = text;
		Locale = locale;
		CreatedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The comment this is a version of.</summary>
	public TinyId CommentId { get; private init; }

	/// <summary>1 for the first text, one more for each edit.</summary>
	public int Number { get; private init; }

	/// <summary>The text as the member wrote it.</summary>
	public string Text { get; private init; }

	/// <summary>The language it was written in.</summary>
	public Locale Locale { get; private init; }

	/// <summary>The text in the other official language, once the Worker has supplied it.</summary>
	public string? TranslatedText { get; private set; }

	/// <summary>Where the translation came from. Only <see cref="TranslationSource.Auto" /> today.</summary>
	public TranslationSource? TranslationSource { get; private set; }

	/// <summary>When it was written.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When its comment was deleted, if it was.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	internal static ReportCommentRevision Write(TinyId commentId,
												int number,
												string text,
												Locale locale,
												DateTimeOffset at)
	{
		return new ReportCommentRevision(commentId, number, text, locale, at);
	}

	/// <summary>
	///     Records the Worker's machine translation. A revision is translated once;
	///     a second call leaves the first translation in place.
	/// </summary>
	public void SupplyAutoTranslation(string translated)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(translated);

		if (TranslatedText is not null)
		{
			return;
		}

		TranslatedText = translated;
		TranslationSource = Reporting.TranslationSource.Auto;
	}

	internal void Delete(DateTimeOffset at)
	{
		Deleted ??= at;
	}
}
