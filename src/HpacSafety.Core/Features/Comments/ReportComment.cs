namespace HpacSafety.Core.Features.Comments;

/// <summary>
///     A member's comment on a published report (ADR-0114). Its author is the
///     opaque token subject and nothing else. Its text lives in an ordered list of
///     immutable <see cref="Revisions" />, and the newest is what readers see.
///     Nothing is ever erased: the author deletes by soft-deleting, and a reviewer
///     hides.
/// </summary>
public class ReportComment
{
	/// <summary>The longest comment text a member may write.</summary>
	public const int MaxLength = 2000;

	/// <summary>The longest token subject stored, the same width as every other subject column.</summary>
	public const int SubjectMaxLength = 256;

	private readonly List<ReportCommentRevision> _revisions = [];

	// For EF Core only; see ADR-0019.
#pragma warning disable CS8618 // Every mapped property is set by EF Core immediately after this runs.
	private ReportComment()
	{
	}
#pragma warning restore CS8618

	private ReportComment(TinyId reportId,
						  string authorSubject,
						  DateTimeOffset at)
	{
		Id = TinyId.New();
		ReportId = reportId;
		AuthorSubject = authorSubject;
		CreatedAt = at;
	}

	/// <summary>Surrogate key.</summary>
	public TinyId Id { get; private init; }

	/// <summary>The published report it is on.</summary>
	public TinyId ReportId { get; private init; }

	/// <summary>
	///     Who wrote it, as their token's subject. Opaque, never returned by the
	///     public API, and not a key: there is no user table (ADR-0065).
	/// </summary>
	public string AuthorSubject { get; private init; }

	/// <summary>When it was first posted.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When a reviewer hid it, if one did.</summary>
	public DateTimeOffset? HiddenAt { get; private set; }

	/// <summary>The reviewer who hid it, as their token's subject.</summary>
	public string? HiddenBySubject { get; private set; }

	/// <summary>When its author deleted it, if they did.</summary>
	public DateTimeOffset? Deleted { get; private set; }

	/// <summary>Every version of its text, oldest first.</summary>
	public IReadOnlyList<ReportCommentRevision> Revisions => _revisions;

	/// <summary>The text readers see: the newest revision.</summary>
	public ReportCommentRevision Current => _revisions.MaxBy(revision => revision.Number)
											?? throw new InvalidOperationException("A comment always has a revision.");

	/// <summary>Posts a new comment with its first revision.</summary>
	public static ReportComment Post(TinyId reportId,
									 string authorSubject,
									 string? text,
									 Locale locale,
									 DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(authorSubject);

		var comment = new ReportComment(reportId, authorSubject, at);
		comment._revisions.Add(ReportCommentRevision.Write(comment.Id, 1, Validated(text), locale, at));
		return comment;
	}

	/// <summary>
	///     The author replaces the text. The earlier revision is kept, and the new
	///     one waits for its own translation.
	/// </summary>
	/// <exception cref="CommentNotYoursException">Anyone but the author.</exception>
	public ReportCommentRevision Edit(string subject,
									  string? text,
									  Locale locale,
									  DateTimeOffset at)
	{
		EnsureAuthor(subject);
		EnsureVisible();

		var revision = ReportCommentRevision.Write(Id, Current.Number + 1, Validated(text), locale, at);
		_revisions.Add(revision);
		return revision;
	}

	/// <summary>The author deletes it: the comment and every revision are stamped deleted.</summary>
	/// <exception cref="CommentNotYoursException">Anyone but the author.</exception>
	public void DeleteBy(string subject,
						 DateTimeOffset at)
	{
		EnsureAuthor(subject);

		if (Deleted is not null)
		{
			return;
		}

		Deleted = at;

		foreach (var revision in _revisions)
		{
			revision.Delete(at);
		}
	}

	/// <summary>
	///     A reviewer hides it from every public read. Who may do this is the
	///     caller's authorization, not the comment's. Hiding twice keeps the first.
	/// </summary>
	public void HideBy(string reviewerSubject,
					   DateTimeOffset at)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reviewerSubject);

		if (HiddenAt is not null)
		{
			return;
		}

		HiddenAt = at;
		HiddenBySubject = reviewerSubject;
	}

	private void EnsureAuthor(string subject)
	{
		if (!string.Equals(subject, AuthorSubject, StringComparison.Ordinal))
		{
			throw new CommentNotYoursException();
		}
	}

	private void EnsureVisible()
	{
		if (Deleted is not null || HiddenAt is not null)
		{
			throw new DomainRuleViolationException("A deleted or hidden comment cannot be edited.");
		}
	}

	private static string Validated(string? text)
	{
		var trimmed = text?.Trim() ?? string.Empty;

		if (trimmed.Length == 0)
		{
			throw new DomainRuleViolationException("A comment needs some text.");
		}

		if (trimmed.Length > MaxLength)
		{
			throw new DomainRuleViolationException($"A comment is at most {MaxLength} characters.");
		}

		return trimmed;
	}
}
