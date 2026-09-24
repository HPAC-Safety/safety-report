namespace HpacSafety.Api.PublicReports;

/// <summary>
///     One published report as the public sees it. This is the whole allowlist:
///     the opaque ID, the two approved summary texts, when it was published, and
///     how many visible comments it has (REQ-MOD-036, REQ-COM-014, CON-DP-011).
///     Nothing else about a report ever leaves through the public API.
/// </summary>
public sealed record PublicReportView(
	string Id,
	string AiSummaryEn,
	string AiSummaryFr,
	DateTimeOffset PublishedAt,
	int CommentCount);

/// <summary>
///     One page of the public feed, newest published first. <see cref="Next" />
///     is the opaque cursor that continues it, or null on the last page
///     (REQ-MOD-037).
/// </summary>
public sealed record PublicReportPage(
	IReadOnlyList<PublicReportView> Items,
	string? Next);

/// <summary>
///     One visible comment as a reader sees it (ADR-0114): its current text in the
///     language it was written in, the machine translation once there is one, and
///     whether it is the reader's own. Never who wrote it.
/// </summary>
public sealed record PublicCommentView(
	string Id,
	string Text,
	string Locale,
	string? TranslatedText,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	bool Edited,
	bool IsMine);

/// <summary>What a member writes: the text, and the language of the page they wrote it on.</summary>
public sealed record WriteCommentRequest(string? Text, string? Locale);
