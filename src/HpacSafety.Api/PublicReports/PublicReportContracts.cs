using HpacSafety.Api.Admin;

namespace HpacSafety.Api.PublicReports;

/// <summary>
///     One published report as the public sees it. This is the whole allowlist:
///     the opaque ID, the two approved summary texts, when it was published, how
///     many visible comments it has, and how many attachments this viewer may see
///     (REQ-MOD-036, REQ-COM-014, CON-DP-011). <see cref="AttachmentCount" /> is
///     viewer-scoped (issue #427, decisions 1-2): the public count for an
///     anonymous visitor or a <c>User</c>, the full non-deleted count for a
///     signed-in <c>SafetyOfficer</c>/<c>Administrator</c>. It carries no other
///     attachment detail — no ids, kinds, names, or links. Nothing else about a
///     report ever leaves through the public API.
/// </summary>
public sealed record PublicReportView(
	string Id,
	string AiSummaryEn,
	string AiSummaryFr,
	DateTimeOffset PublishedAt,
	int CommentCount,
	int AttachmentCount);

/// <summary>
///     A published report's own page: the feed item's allowlist plus the
///     attachments this viewer may see. For the public, <see cref="Media" /> is
///     each public image, video, or document as an opaque id and a kind and
///     nothing else — no name, size, type, key, or URL (REQ-MOD-036, REQ-MED-025).
///     For a signed-in <c>SafetyOfficer</c>/<c>Administrator</c>,
///     <see cref="StaffAttachments" /> additionally carries every attachment —
///     public or not — each marked with its state and public visibility, the same
///     vocabulary the admin report page uses (issue #427, decisions 1, 4, 14); it
///     is null for anyone else. Either way the page asks for each file's link
///     separately, so a link is minted only when it is about to be used and
///     expires on its own (ADR-0117). Only here, never in the feed,
///     <see cref="Language" /> is the locale code the reporter wrote the report in
///     (<c>en-CA</c> or <c>fr-CA</c>), so the page can say a summary was translated
///     from it; the owner accepted that this is a slight identifying hint
///     (#682, ADR-0176).
/// </summary>
public sealed record PublicReportDetail(
	string Id,
	string AiSummaryEn,
	string AiSummaryFr,
	DateTimeOffset PublishedAt,
	int CommentCount,
	int AttachmentCount,
	string Language,
	IReadOnlyList<PublicMediaView> Media,
	IReadOnlyList<ReportAttachmentView>? StaffAttachments);

/// <summary>
///     One public file: its opaque id, <c>image</c>, <c>video</c>, or
///     <c>document</c>, and for a document only its coarse format — the extension
///     it downloads with, such as <c>pdf</c> (ADR-0119). Nothing else about it is
///     public.
/// </summary>
public sealed record PublicMediaView(string Id, string Kind, string? Format);

/// <summary>
///     A short-lived link to one public file — inline to an image or video's
///     derivative (REQ-MED-028), or a forced download of a document's original
///     (REQ-MED-039) — and when it stops working, so the page knows to ask again.
/// </summary>
public sealed record PublicMediaLinkView(string Url, DateTimeOffset ExpiresAt);

/// <summary>
///     One page of the public feed, newest submitted first. <see cref="Next" />
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
