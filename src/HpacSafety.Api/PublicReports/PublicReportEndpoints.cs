using System.Buffers.Text;
using System.Text;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.PublicReports;

/// <summary>
///     The anonymous public feed of published reports and each report's own page
///     (#28). Both read the <c>public_reports</c> view, which holds the whole
///     publication invariant, so nothing here decides what is public.
/// </summary>
public static class PublicReportEndpoints
{
	/// <summary>How many reports one feed page carries.</summary>
	public const int PageSize = 20;

	/// <summary>Maps the public report endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapPublicReports(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/v1/public/reports");

		group.MapGet("/", List);
		group.MapGet("/{reportId}", Get);
		group.MapGet("/{reportId}/media/{mediaId}", MediaLink);

		return group;
	}

	/// <summary>
	///     One page of the feed: newest submitted first, a tie broken by report ID
	///     so the order is total and a cursor never skips or repeats a report
	///     (REQ-MOD-037). An unreadable cursor, or one naming a report that is no
	///     longer publishable, starts from the top rather than failing, because it
	///     is only ever a bookmark. Submission time orders the feed and keys the
	///     query's keyset position; it is looked up server-side from the cursor's
	///     report ID, so it is never a field of the response and never travels to
	///     or from the client — each entry still displays <c>published_at</c>.
	/// </summary>
	private static async Task<IResult> List(
		string? after,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		var query = database.PublicReports.AsNoTracking();

		if (Cursor.TryRead(after, out var afterId))
		{
			var position = await database.PublicReports
				.AsNoTracking()
				.Where(report => report.Id == afterId)
				.Select(report => new { report.Id, report.SubmittedAt })
				.SingleOrDefaultAsync(cancellationToken)
				.ConfigureAwait(false);

			// A cursor naming a report that has since been unpublished, deleted, or
			// never existed resolves to nothing here; the feed then starts from the
			// top, the same as an unreadable cursor.
			if (position is not null)
			{
				var submittedAt = position.SubmittedAt;
				var id = position.Id;

				// EF translates only the two-argument string.Compare into SQL, where
				// it uses the same collation as the ORDER BY below, which is what
				// keeps the cursor consistent with the order.
#pragma warning disable CA1309
				query = query.Where(report =>
					report.SubmittedAt < submittedAt
					|| (report.SubmittedAt == submittedAt && string.Compare(report.Id, id) < 0));
#pragma warning restore CA1309
			}
		}

		var rows = await query
			.OrderByDescending(report => report.SubmittedAt)
			.ThenByDescending(report => report.Id)
			.Take(PageSize + 1)
			.Select(report => new PublicReportRow(report.Id, report.AiSummaryEn, report.AiSummaryFr, report.PublishedAt, report.CommentCount))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var page = rows.Take(PageSize).ToList();
		var next = rows.Count > PageSize ? Cursor.Write(page[^1].Id) : null;

		var items = page
			.Select(row => new PublicReportView(row.Id, row.AiSummaryEn, row.AiSummaryFr, row.PublishedAt, row.CommentCount))
			.ToList();

		return Results.Ok(new PublicReportPage(items, next));
	}

	/// <summary>
	///     One publishable report, or 404. An unknown ID and a report that is not
	///     public get the same empty 404, so the response never says which it was
	///     (REQ-MOD-038).
	/// </summary>
	private static async Task<IResult> Get(
		string reportId,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		// Any text is only a lookup key here: an ID that is not one simply has no row.
		var report = await database.PublicReports
			.AsNoTracking()
			.Where(candidate => candidate.Id == reportId)
			.Select(candidate => new PublicReportView(candidate.Id, candidate.AiSummaryEn, candidate.AiSummaryFr, candidate.PublishedAt, candidate.CommentCount))
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		// The view holds the whole public-media rule (ADR-0117); this only lists it.
		var media = await database.PublicReportMedia
			.AsNoTracking()
			.Where(file => file.ReportId == reportId)
			.OrderBy(file => file.UploadedAt)
			.ThenBy(file => file.Id)
			.Select(file => new { file.Id, file.Kind, file.ContentType })
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(new PublicReportDetail(
			report.Id, report.AiSummaryEn, report.AiSummaryFr, report.PublishedAt, report.CommentCount,
			[.. media.Select(file => new PublicMediaView(file.Id, EnumCode.Of(file.Kind), FormatOf(file.Kind, file.ContentType)))]));
	}

	/// <summary>A document's coarse format, the extension it downloads with; none for an image or video.</summary>
	private static string? FormatOf(AttachmentKind kind,
									string contentType)
	{
		return kind is AttachmentKind.Document && MediaType.TryParse(contentType, out var type) ? type.Extension : null;
	}

	/// <summary>
	///     A short-lived link for anyone: inline to one public image or video's
	///     derivative, or a forced download of one public document's original. It
	///     is 404 for anything <c>public_report_media</c> does not hold — a hidden
	///     file, an unpublished or deleted report, an unvalidated document, an image
	///     or video original — indistinguishably from an unknown id (REQ-MED-026 to
	///     REQ-MED-029, REQ-MED-038 to REQ-MED-040).
	/// </summary>
	private static async Task<IResult> MediaLink(
		string reportId,
		string mediaId,
		HttpContext context,
		HpacSafetyDbContext database,
		PublicMediaLink links,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		var file = await database.PublicReportMedia
			.AsNoTracking()
			.Where(candidate => candidate.Id == mediaId && candidate.ReportId == reportId)
			.Select(candidate => new { candidate.Kind, candidate.ContentType, candidate.StrippedBlobKey, candidate.DocumentBlobKey })
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (file is null
			|| !MediaType.TryParse(file.ContentType, out var original))
		{
			return Results.NotFound();
		}

		Uri url;

		if (file.Kind is AttachmentKind.Document)
		{
			url = await links.CreateDocumentDownloadUrl(
				TinyId.Parse(mediaId), BlobKey.Parse(file.DocumentBlobKey), original, BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			// An image's derivative is its stripped form (a HEIC becomes a JPEG); a
			// video's is always an MP4 (ADR-0122).
			var served = original.DerivativeForm ?? original;

			url = await links.CreateUrl(
				BlobKey.Parse(file.StrippedBlobKey), served.ContentType, BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);
		}

		context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

		// A link is minted per request and never cached by anyone else.
		context.Response.Headers.CacheControl = "no-store";

		return Results.Ok(new PublicMediaLinkView(url.ToString(), clock.GetUtcNow().Add(BlobUrlLifetime.Maximum)));
	}

	/// <summary>
	///     One <c>public_reports</c> row read for a feed page: exactly the public
	///     DTO's fields (#570).
	/// </summary>
	private sealed record PublicReportRow(
		string Id,
		string AiSummaryEn,
		string AiSummaryFr,
		DateTimeOffset PublishedAt,
		int CommentCount);

	/// <summary>
	///     The feed's keyset position: the last page's last report ID, an opaque
	///     value already public on its own page, written as one URL-safe token.
	///     It names a place in a public list and carries nothing else — in
	///     particular never a timestamp. <see cref="List" /> looks the ID's
	///     <c>submitted_at</c> up server-side to resolve a position; submission
	///     time itself never reaches the client in either direction.
	/// </summary>
	private static class Cursor
	{
		/// <summary>
		///     The longest a token can be: base64url, no padding, of a
		///     <see cref="TinyId.Length" />-byte string — rounded up rather than
		///     computed exactly, since this only bounds a malformed token out early.
		/// </summary>
		private static readonly int MaxTokenLength = ((TinyId.Length + 2) / 3) * 4;

		public static string Write(string id)
		{
			return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(id));
		}

		public static bool TryRead(string? token, out string id)
		{
			id = string.Empty;

			if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength || !Base64Url.IsValid(token))
			{
				return false;
			}

			var plain = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));

			if (!TinyId.TryParse(plain, out _))
			{
				return false;
			}

			id = plain;
			return true;
		}
	}
}
