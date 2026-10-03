using System.Security.Cryptography;
using System.Text;
using HpacSafety.Api.Admin;
using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.PublicReports;

/// <summary>
///     A reporter's own report before it is published (#820, ADR-0196). The browser
///     that filed a report holds its receipt; the report stores only the receipt's
///     SHA-256 hash, so the server recognises a receipt it is shown and no more. A
///     holder sees what the public will see once the report is published, plus its
///     latest summary as a draft. Every read goes through the <c>own_reports</c> and
///     <c>own_report_media</c> views, so nothing here decides what is visible, and a
///     receipt that does not match answers exactly as an unknown report does.
/// </summary>
/// <remarks>
///     Every request is a <c>POST</c> whose body carries the receipt: a receipt in
///     an address would land in history, logs, and referrers. Nothing here is
///     authenticated by membership, because the receipt is the credential, and
///     nothing records who asked.
/// </remarks>
internal static class OwnReportEndpoints
{
	/// <summary>The most receipts one lookup reads; a browser files only so many reports.</summary>
	public const int MaxReceipts = 50;

	/// <summary>Maps the receipt endpoints onto the public reports group.</summary>
	/// <param name="group">The <c>/api/v1/public/reports</c> group.</param>
	public static void Map(RouteGroupBuilder group)
	{
		ArgumentNullException.ThrowIfNull(group);

		group.MapPost("/own", List);
		group.MapPost("/own/{reportId}", Get);
		group.MapPost("/own/{reportId}/media/{mediaId}", MediaLink);
	}

	/// <summary>
	///     The holder's own reports, newest submitted first, and every named report
	///     that is not (or no longer) theirs, so the browser can drop its receipt.
	/// </summary>
	private static async Task<IResult> List(
		OwnReportsRequest? request,
		HttpContext context,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		NoStore(context);

		if (request?.Receipts is not { } named)
		{
			return Results.Problem("A lookup must name its receipts.", statusCode: StatusCodes.Status400BadRequest);
		}

		if (named.Count > MaxReceipts)
		{
			return Results.Problem($"A lookup names at most {MaxReceipts} receipts.", statusCode: StatusCodes.Status400BadRequest);
		}

		var asked = new List<(string Echo, string? Id, byte[]? Hash)>();

		foreach (var entry in named)
		{
			var echo = entry?.ReportId ?? string.Empty;

			if (entry is not null
				&& TinyId.TryParse(entry.ReportId, out var id)
				&& BrowserReceipt.TryHash(entry.Receipt, out var hash))
			{
				asked.Add((echo, id.Value, Encoding.ASCII.GetBytes(hash)));
			}
			else
			{
				asked.Add((echo, null, null));
			}
		}

		var ids = asked.Where(entry => entry.Id is not null).Select(entry => entry.Id!).Distinct().ToList();

		var rows = ids.Count == 0
			? []
			: await database.OwnReports
				.AsNoTracking()
				.Where(report => ids.Contains(report.Id))
				.ToListAsync(cancellationToken)
				.ConfigureAwait(false);

		var matched = new Dictionary<string, OwnReport>();

		foreach (var (_, id, hash) in asked)
		{
			if (id is not null
				&& hash is not null
				&& rows.SingleOrDefault(row => row.Id == id) is { } row
				&& Matches(row, hash))
			{
				matched[id] = row;
			}
		}

		var settled = asked
			.Where(entry => entry.Id is null || !matched.ContainsKey(entry.Id))
			.Select(entry => entry.Echo)
			.Distinct()
			.ToList();

		var items = matched.Values
			.OrderByDescending(row => row.SubmittedAt)
			.ThenByDescending(row => row.Id, StringComparer.Ordinal)
			.Select(row => new OwnReportView(row.Id, row.SubmittedAt, row.ForPublication, row.AiSummaryEn, row.AiSummaryFr, row.AttachmentCount))
			.ToList();

		return Results.Ok(new OwnReportsResponse(items, settled));
	}

	/// <summary>
	///     The holder's own report page, or 404. An unknown report, a report that is
	///     public or deleted, and a receipt that does not match all answer the same
	///     empty 404.
	/// </summary>
	private static async Task<IResult> Get(
		string reportId,
		OwnReceiptRequest? request,
		HttpContext context,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		NoStore(context);

		var report = await Find(reportId, request, database, cancellationToken).ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		// The view holds the whole media rule, shared with the public view; this
		// only lists it.
		var media = await database.OwnReportMedia
			.AsNoTracking()
			.Where(file => file.ReportId == report.Id)
			.OrderBy(file => file.UploadedAt)
			.ThenBy(file => file.Id)
			.Select(file => new { file.Id, file.Kind, file.ContentType })
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(new OwnReportDetail(
			report.Id, report.SubmittedAt, report.ForPublication, report.AiSummaryEn, report.AiSummaryFr, report.AttachmentCount, report.Language,
			[.. media.Select(file => new PublicMediaView(file.Id, EnumCode.Of(file.Kind), ReportEndpoints.FormatOf(file.Kind, file.ContentType)))]));
	}

	/// <summary>
	///     A short-lived link to one of the holder's files, minted by the same code
	///     that mints a public file's. 404 for any file <c>own_report_media</c> does
	///     not hold, and for any receipt that does not match.
	/// </summary>
	private static async Task<IResult> MediaLink(
		string reportId,
		string mediaId,
		OwnReceiptRequest? request,
		HttpContext context,
		HpacSafetyDbContext database,
		PublicMediaLink links,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		NoStore(context);

		var report = await Find(reportId, request, database, cancellationToken).ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		var file = await database.OwnReportMedia
			.AsNoTracking()
			.Where(candidate => candidate.Id == mediaId && candidate.ReportId == report.Id)
			.Select(candidate => new { candidate.Kind, candidate.ContentType, candidate.StrippedBlobKey, candidate.DocumentBlobKey })
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (file is null)
		{
			return Results.NotFound();
		}

		return await PublicReportEndpoints.MintMediaLink(
			file.Kind, file.ContentType, file.StrippedBlobKey, file.DocumentBlobKey, mediaId, context, links, clock, cancellationToken)
			.ConfigureAwait(false);
	}

	private static async Task<OwnReport?> Find(
		string reportId,
		OwnReceiptRequest? request,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(reportId, out var id)
			|| !BrowserReceipt.TryHash(request?.Receipt, out var hash))
		{
			return null;
		}

		var report = await database.OwnReports
			.AsNoTracking()
			.Where(candidate => candidate.Id == id.Value)
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		return report is not null && Matches(report, Encoding.ASCII.GetBytes(hash)) ? report : null;
	}

	/// <summary>
	///     True when the receipt's hash is the stored one. Compared in constant time,
	///     so a mismatch says nothing about how much of a hash matched.
	/// </summary>
	private static bool Matches(OwnReport report, byte[] hash)
	{
		return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(report.ReceiptHash), hash);
	}

	/// <summary>A holder's report is never cached by anyone else.</summary>
	private static void NoStore(HttpContext context)
	{
		context.Response.Headers.CacheControl = "no-store";
	}
}
