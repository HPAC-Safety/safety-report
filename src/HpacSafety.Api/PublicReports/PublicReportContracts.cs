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
///     A published report's own page: the feed item's allowlist plus each public
///     image or video, as an opaque id and a kind and nothing else — no name,
///     size, type, key, or URL (REQ-MOD-036, REQ-MED-025). The page asks for each
///     file's link separately, so a link is minted only when it is about to be
///     used and expires on its own (ADR-0117).
/// </summary>
public sealed record PublicReportDetail(
	string Id,
	string AiSummaryEn,
	string AiSummaryFr,
	DateTimeOffset PublishedAt,
	int CommentCount,
	IReadOnlyList<PublicMediaView> Media);

/// <summary>One public image or video: its opaque id, and <c>image</c> or <c>video</c>.</summary>
public sealed record PublicMediaView(string Id, string Kind);

/// <summary>
///     A short-lived, inline link to one public file's derivative (REQ-MED-028),
///     and when it stops working, so the page knows to ask again.
/// </summary>
public sealed record PublicMediaLinkView(string Url, DateTimeOffset ExpiresAt);

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
