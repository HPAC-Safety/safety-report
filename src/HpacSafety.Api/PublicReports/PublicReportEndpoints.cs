using System.Buffers.Text;
using System.Text;
using HpacSafety.Api.Admin;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

	/// <summary>The longest search box text the API will read; anything past this is ignored.</summary>
	public const int MaxSearchQueryLength = 200;

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
	///     One page of the feed. A blank search box is newest submitted first, a
	///     tie broken by report ID, exactly as before (REQ-MOD-037). A non-blank
	///     <paramref name="q" /> instead fuzzy-searches the approved published
	///     summary and visible member comments, in <paramref name="locale" />
	///     only, best match first (#574, ADR-0157); the query text is never
	///     logged. Either way, an unreadable cursor, or one naming a report that
	///     is no longer publishable or no longer matches, starts from the top
	///     rather than failing, because it is only ever a bookmark.
	/// </summary>
	private static async Task<IResult> List(
		string? after,
		string? q,
		string? locale,
		HpacSafetyDbContext database,
		HttpContext context,
		IOptions<HpacAuthenticationOptions> authOptions,
		CancellationToken cancellationToken)
	{
		var isStaff = IsStaff(context, authOptions);
		var trimmed = q?.Trim();

		if (!string.IsNullOrEmpty(trimmed))
		{
			return await Search(
				trimmed.Length > MaxSearchQueryLength ? trimmed[..MaxSearchQueryLength] : trimmed,
				locale, after, isStaff, database, cancellationToken).ConfigureAwait(false);
		}

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
			.Select(report => new PublicReportRow(
				report.Id, report.AiSummaryEn, report.AiSummaryFr, report.PublishedAt, report.CommentCount,
				report.PublicAttachmentCount, report.FullAttachmentCount))
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var page = rows.Take(PageSize).ToList();
		var next = rows.Count > PageSize ? Cursor.Write(page[^1].Id) : null;

		var items = page
			.Select(row => new PublicReportView(
				row.Id, row.AiSummaryEn, row.AiSummaryFr, row.PublishedAt, row.CommentCount,
				isStaff ? row.FullAttachmentCount : row.PublicAttachmentCount))
			.ToList();

		return Results.Ok(new PublicReportPage(items, next));
	}

	/// <summary>
	///     One page of best-match results for a non-blank search box
	///     (REQ-MOD-140 to REQ-MOD-149). <c>search_public_reports</c> is the one
	///     read: it joins only <c>public_reports</c> and
	///     <c>public_report_comments</c>, the two views that already hold the
	///     whole publication and visibility invariant, so nothing not already
	///     public can reach a match. The cursor still carries only a report ID
	///     (ADR-0153); the function resolves its own rank position by looking
	///     that ID back up against itself, so a rank score never travels to or
	///     from the client either.
	/// </summary>
	private static async Task<IResult> Search(
		string q,
		string? localeCode,
		string? after,
		bool isStaff,
		HpacSafetyDbContext database,
		CancellationToken cancellationToken)
	{
		var locale = Locale.TryParse(localeCode, out var parsed) ? parsed : Locale.EnCa;
		var afterId = Cursor.TryRead(after, out var id) ? id : null;

		var rows = await database.Database
			.SqlQuery<PublicReportSearchRow>(
				$"""
				 SELECT id AS "Id", ai_summary_en AS "AiSummaryEn", ai_summary_fr AS "AiSummaryFr",
				        published_at AS "PublishedAt", comment_count AS "CommentCount",
				        public_attachment_count AS "PublicAttachmentCount",
				        full_attachment_count AS "FullAttachmentCount"
				 FROM search_public_reports({q}, {locale.Code}, {afterId}, {PageSize + 1})
				 """)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var page = rows.Take(PageSize).ToList();
		var next = rows.Count > PageSize ? Cursor.Write(page[^1].Id) : null;

		var items = page
			.Select(row => new PublicReportView(
				row.Id, row.AiSummaryEn, row.AiSummaryFr, row.PublishedAt, row.CommentCount,
				isStaff ? row.FullAttachmentCount : row.PublicAttachmentCount))
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
		HttpContext context,
		IOptions<HpacAuthenticationOptions> authOptions,
		CancellationToken cancellationToken)
	{
		var isStaff = IsStaff(context, authOptions);

		// Any text is only a lookup key here: an ID that is not one simply has no row.
		var report = await database.PublicReports
			.AsNoTracking()
			.Where(candidate => candidate.Id == reportId)
			.Select(candidate => new { candidate.Id, candidate.AiSummaryEn, candidate.AiSummaryFr, candidate.PublishedAt, candidate.CommentCount, candidate.PublicAttachmentCount, candidate.FullAttachmentCount })
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

		IReadOnlyList<ReportAttachmentView>? staffAttachments = null;

		if (isStaff && TinyId.TryParse(reportId, out var parsedReportId))
		{
			// Reused from the admin report page (issue #427, decision 14): every
			// attachment — public or not — with its state and public visibility, in
			// the same vocabulary. Not an audited read: only opening an attachment's
			// own bytes is (see AttachmentEndpoints).
			var source = await database.Reports
				.AsNoTracking()
				.Include(candidate => candidate.Files)
				.SingleOrDefaultAsync(candidate => candidate.Id == parsedReportId, cancellationToken)
				.ConfigureAwait(false);

			if (source is not null)
			{
				staffAttachments = [.. source.Files
					.OrderBy(file => file.UploadedAt)
					.ThenBy(file => file.Id)
					.Select(file => new ReportAttachmentView(
						file.Id.Value, EnumCode.Of(file.Kind), ReportEndpoints.AttachmentState(file), ReportEndpoints.Visibility(source, file),
						ReportEndpoints.FormatOf(file.Kind, file.ContentType)))];
			}
		}

		return Results.Ok(new PublicReportDetail(
			report.Id, report.AiSummaryEn, report.AiSummaryFr, report.PublishedAt, report.CommentCount,
			isStaff ? report.FullAttachmentCount : report.PublicAttachmentCount,
			[.. media.Select(file => new PublicMediaView(file.Id, EnumCode.Of(file.Kind), ReportEndpoints.FormatOf(file.Kind, file.ContentType)))],
			staffAttachments));
	}

	/// <summary>
	///     True for a validated bearer token carrying <c>SafetyOfficer</c> or
	///     <c>Administrator</c> (decision 4). The public endpoints stay anonymous;
	///     JwtBearer is the default scheme, so a sent token is still authenticated
	///     here even though nothing requires one.
	/// </summary>
	private static bool IsStaff(HttpContext context,
								IOptions<HpacAuthenticationOptions> authOptions)
	{
		if (context.User.Identity?.IsAuthenticated != true)
		{
			return false;
		}

		var role = MemberRoles.EffectiveRole(context.User, authOptions.Value.RoleClaimType);
		return role is MemberRole.SafetyOfficer or MemberRole.Administrator;
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
		int CommentCount,
		int PublicAttachmentCount,
		int FullAttachmentCount);

	/// <summary>One <c>search_public_reports</c> row, aliased to match these property names exactly.</summary>
	private sealed record PublicReportSearchRow(
		string Id,
		string AiSummaryEn,
		string AiSummaryFr,
		DateTimeOffset PublishedAt,
		int CommentCount,
		int PublicAttachmentCount,
		int FullAttachmentCount);

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
