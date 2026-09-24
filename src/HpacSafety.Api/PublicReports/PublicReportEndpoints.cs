using System.Buffers.Text;
using System.Globalization;
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
	///     One page of the feed: newest published first, a tie broken by report ID
	///     so the order is total and a cursor never skips or repeats a report
	///     (REQ-MOD-037). An unreadable cursor starts from the top rather than
	///     failing, because it is only ever a bookmark.
	/// </summary>
	private static async Task<IResult> List(
		string? after,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		var query = database.PublicReports.AsNoTracking();

		if (Cursor.TryRead(after, out var publishedAt, out var id))
		{
			// EF translates only the two-argument string.Compare into SQL, where it
			// uses the same collation as the ORDER BY below, which is what keeps
			// the cursor consistent with the order.
#pragma warning disable CA1309
			query = query.Where(report =>
				report.PublishedAt < publishedAt
				|| (report.PublishedAt == publishedAt && string.Compare(report.Id, id) < 0));
#pragma warning restore CA1309
		}

		var rows = await query
			.OrderByDescending(report => report.PublishedAt)
			.ThenByDescending(report => report.Id)
			.Take(PageSize + 1)
			.Select(report => new PublicReportView(report.Id, report.AiSummaryEn, report.AiSummaryFr, report.PublishedAt, report.CommentCount))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var items = rows.Take(PageSize).ToList();
		var next = rows.Count > PageSize ? Cursor.Write(items[^1]) : null;

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
			.Select(file => new { file.Id, file.Kind })
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(new PublicReportDetail(
			report.Id, report.AiSummaryEn, report.AiSummaryFr, report.PublishedAt, report.CommentCount,
			[.. media.Select(file => new PublicMediaView(file.Id, EnumCode.Of(file.Kind)))]));
	}

	/// <summary>
	///     A short-lived, inline link to one public image or video's derivative, for
	///     anyone, or 404 for anything <c>public_report_media</c> does not hold — a
	///     hidden file, an unpublished or deleted report, a document, an original —
	///     indistinguishably from an unknown id (REQ-MED-026 to REQ-MED-029).
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
			.Select(candidate => new { candidate.ContentType, candidate.StrippedBlobKey })
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (file is null
			|| !MediaType.TryParse(file.ContentType, out var original))
		{
			return Results.NotFound();
		}

		// An image's derivative is its stripped form (a HEIC becomes a JPEG); a
		// video's is remuxed into the container it arrived in (ADR-0094).
		var served = original.StrippedForm ?? original;

		var url = await links.CreateUrl(
			BlobKey.Parse(file.StrippedBlobKey), served.ContentType, BlobUrlLifetime.Maximum, cancellationToken).ConfigureAwait(false);

		context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

		// A link is minted per request and never cached by anyone else.
		context.Response.Headers.CacheControl = "no-store";

		return Results.Ok(new PublicMediaLinkView(url.ToString(), clock.GetUtcNow().Add(BlobUrlLifetime.Maximum)));
	}

	/// <summary>
	///     The feed's keyset position, the last report's publication time and ID,
	///     written as one opaque URL-safe token. It names a place in a public list
	///     and carries nothing that is not already public.
	/// </summary>
	private static class Cursor
	{
		public static string Write(PublicReportView last)
		{
			var plain = $"{last.PublishedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}.{last.Id}";
			return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(plain));
		}

		public static bool TryRead(string? token,
								   out DateTimeOffset publishedAt,
								   out string id)
		{
			publishedAt = default;
			id = string.Empty;

			if (string.IsNullOrEmpty(token) || token.Length > 64 || !Base64Url.IsValid(token))
			{
				return false;
			}

			var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token)).Split('.');

			// NumberStyles.None admits no sign, so a parsed tick count is never negative.
			if (parts.Length != 2
				|| !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
				|| ticks > DateTimeOffset.MaxValue.UtcTicks
				|| !TinyId.TryParse(parts[1], out _))
			{
				return false;
			}

			publishedAt = new DateTimeOffset(ticks, TimeSpan.Zero);
			id = parts[1];
			return true;
		}
	}
}
