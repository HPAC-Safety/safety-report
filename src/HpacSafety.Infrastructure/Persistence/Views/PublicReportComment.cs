using HpacSafety.Core;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>public_report_comments</c> view: a comment that is
///     neither deleted nor hidden, on a report in <c>public_reports</c>, with its
///     current revision (ADR-0114). <see cref="AuthorSubject" /> is here only so
///     the API can tell a reader which comments are theirs; it is never
///     serialized.
/// </summary>
public sealed class PublicReportComment
{
	/// <summary>The comment's ID.</summary>
	public string Id { get; private init; } = string.Empty;

	/// <summary>The report it is on.</summary>
	public string ReportId { get; private init; } = string.Empty;

	/// <summary>Its author's token subject. Compared on the server, never returned.</summary>
	public string AuthorSubject { get; private init; } = string.Empty;

	/// <summary>The current text, as written.</summary>
	public string Text { get; private init; } = string.Empty;

	/// <summary>The language the current text was written in.</summary>
	public Locale Locale { get; private init; } = Locale.EnCa;

	/// <summary>The current text in the other language, once the Worker has supplied it.</summary>
	public string? TranslatedText { get; private init; }

	/// <summary>When the comment was first posted.</summary>
	public DateTimeOffset CreatedAt { get; private init; }

	/// <summary>When the current text was written.</summary>
	public DateTimeOffset UpdatedAt { get; private init; }

	/// <summary>Whether the comment has more than one revision.</summary>
	public bool Edited { get; private init; }
}
