using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Comments;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Views;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.PublicReports;

/// <summary>
///     Members' comments on a published report (ADR-0114). Anyone reads them; any
///     signed-in member posts; only the author edits or deletes; a reviewer hides.
///     Every route answers 404 unless the report is in <c>public_reports</c> right
///     now, so a comment can neither be written to nor read from a report the
///     public cannot see.
/// </summary>
public static class CommentEndpoints
{
	/// <summary>Maps the comment endpoints.</summary>
	/// <param name="app">The route builder.</param>
	public static IEndpointRouteBuilder MapComments(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var comments = app.MapGroup("/api/v1/public/reports/{reportId}/comments");

		comments.MapGet("/", List);
		comments.MapPost("/", Post).RequireAuthorization(HpacPolicies.Member);
		comments.MapPut("/{commentId}", Edit).RequireAuthorization(HpacPolicies.Member);
		comments.MapDelete("/{commentId}", Delete).RequireAuthorization(HpacPolicies.Member);

		app.MapPost("/api/admin/comments/{commentId}/hide", Hide).RequireAuthorization(HpacPolicies.Reviewer);

		return app;
	}

	/// <summary>
	///     A public report's visible comments, oldest first. A bearer token, when
	///     one is sent, is used only to mark the reader's own (REQ-COM-007).
	/// </summary>
	private static async Task<IResult> List(string reportId,
											HpacSafetyDbContext database,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		if (!await IsPublic(database, reportId, cancellationToken).ConfigureAwait(false))
		{
			return Results.NotFound();
		}

		var reader = ReaderOf(context);
		var rows = await database.PublicReportComments
			.AsNoTracking()
			.Where(comment => comment.ReportId == reportId)
			.OrderBy(comment => comment.CreatedAt)
			.ThenBy(comment => comment.Id)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(rows.ConvertAll(row => View(row, reader)));
	}

	/// <summary>
	///     Posts a comment and queues its translation in the same save. No
	///     translation provider is called here (REQ-COM-006).
	/// </summary>
	private static async Task<IResult> Post(string reportId,
											WriteCommentRequest request,
											HpacSafetyDbContext database,
											TimeProvider clock,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!await IsPublic(database, reportId, cancellationToken).ConfigureAwait(false))
		{
			return Results.NotFound();
		}

		if (!Locale.TryParse(request.Locale, out var locale))
		{
			return Invalid("Say which language the comment is written in: en-CA or fr-CA.");
		}

		var at = clock.GetUtcNow();
		ReportComment comment;

		try
		{
			comment = ReportComment.Post(TinyId.Parse(reportId), SubjectOf(context), request.Text, locale, at);
		}
		catch (DomainRuleViolationException refusal)
		{
			return Invalid(refusal.Message);
		}

		database.ReportComments.Add(comment);
		Enqueue(database, comment, comment.Current.Id, at);
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		var view = await Read(database, comment.Id, SubjectOf(context), cancellationToken).ConfigureAwait(false);
		return Results.Created($"/api/v1/public/reports/{reportId}/comments/{comment.Id.Value}", view);
	}

	/// <summary>The author replaces the text with a new revision (REQ-COM-008).</summary>
	private static async Task<IResult> Edit(string reportId,
											string commentId,
											WriteCommentRequest request,
											HpacSafetyDbContext database,
											TimeProvider clock,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		var comment = await LoadVisible(database, reportId, commentId, cancellationToken).ConfigureAwait(false);

		if (comment is null)
		{
			return Results.NotFound();
		}

		if (!Locale.TryParse(request.Locale, out var locale))
		{
			return Invalid("Say which language the comment is written in: en-CA or fr-CA.");
		}

		var at = clock.GetUtcNow();

		try
		{
			var revision = comment.Edit(SubjectOf(context), request.Text, locale, at);
			Enqueue(database, comment, revision.Id, at);
		}
		catch (CommentNotYoursException)
		{
			return Results.Forbid();
		}
		catch (DomainRuleViolationException refusal)
		{
			return Invalid(refusal.Message);
		}

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Results.Ok(await Read(database, comment.Id, SubjectOf(context), cancellationToken).ConfigureAwait(false));
	}

	/// <summary>The author deletes it: a soft delete, audited (REQ-COM-009).</summary>
	private static async Task<IResult> Delete(string reportId,
											  string commentId,
											  HpacSafetyDbContext database,
											  TimeProvider clock,
											  HttpContext context,
											  CancellationToken cancellationToken)
	{
		var comment = await LoadVisible(database, reportId, commentId, cancellationToken).ConfigureAwait(false);

		if (comment is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();
		var subject = SubjectOf(context);

		try
		{
			comment.DeleteBy(subject, at);
		}
		catch (CommentNotYoursException)
		{
			return Results.Forbid();
		}

		database.AuditLog.Add(new AuditLogEntry(subject, AuditAction.DeletedComment, "ReportComment", comment.Id, at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Results.NoContent();
	}

	/// <summary>
	///     A reviewer hides a comment from every public read. Audited without its
	///     text (REQ-COM-011). A comment already hidden or deleted is not found.
	/// </summary>
	private static async Task<IResult> Hide(string commentId,
											HpacSafetyDbContext database,
											TimeProvider clock,
											HttpContext context,
											CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(commentId, out var id))
		{
			return Results.NotFound();
		}

		var comment = await database.ReportComments
			.SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.HiddenAt == null, cancellationToken)
			.ConfigureAwait(false);

		if (comment is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();
		var subject = SubjectOf(context);
		comment.HideBy(subject, at);
		database.AuditLog.Add(new AuditLogEntry(subject, AuditAction.HidComment, "ReportComment", comment.Id, at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Results.NoContent();
	}

	private static Task<bool> IsPublic(HpacSafetyDbContext database,
									   string reportId,
									   CancellationToken cancellationToken)
	{
		return database.PublicReports.AnyAsync(report => report.Id == reportId, cancellationToken);
	}

	/// <summary>A comment on a public report that is neither deleted nor hidden, with its revisions.</summary>
	private static async Task<ReportComment?> LoadVisible(HpacSafetyDbContext database,
														  string reportId,
														  string commentId,
														  CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(commentId, out var id)
			|| !await IsPublic(database, reportId, cancellationToken).ConfigureAwait(false))
		{
			return null;
		}

		var reportKey = TinyId.Parse(reportId);
		return await database.ReportComments
			.Include(comment => comment.Revisions)
			.SingleOrDefaultAsync(comment => comment.Id == id && comment.ReportId == reportKey && comment.HiddenAt == null, cancellationToken)
			.ConfigureAwait(false);
	}

	private static async Task<PublicCommentView> Read(HpacSafetyDbContext database,
													  TinyId commentId,
													  string reader,
													  CancellationToken cancellationToken)
	{
		var row = await database.PublicReportComments
			.AsNoTracking()
			.SingleAsync(comment => comment.Id == commentId.Value, cancellationToken)
			.ConfigureAwait(false);

		return View(row, reader);
	}

	private static void Enqueue(HpacSafetyDbContext database,
								ReportComment comment,
								TinyId revisionId,
								DateTimeOffset at)
	{
		database.OutboxMessages.Add(new OutboxMessage(comment.Id, OutboxMessageType.TranslateComment, revisionId.Value, at));
	}

	private static PublicCommentView View(PublicReportComment row,
										  string? reader)
	{
		return new PublicCommentView(
			row.Id,
			row.Text,
			row.Locale.Code,
			row.TranslatedText,
			row.CreatedAt,
			row.UpdatedAt,
			row.Edited,
			reader is not null && string.Equals(row.AuthorSubject, reader, StringComparison.Ordinal));
	}

	/// <summary>The signed-in reader's subject, or null for an anonymous one.</summary>
	private static string? ReaderOf(HttpContext context)
	{
		return context.User.Identity?.IsAuthenticated == true ? MemberRoles.SubjectOf(context.User) : null;
	}

	private static string SubjectOf(HttpContext context)
	{
		// Only reached behind the member or reviewer policy, so the token was
		// validated. Every issuer this API accepts sets `sub` (OIDC requires it),
		// and a comment's ownership is that value.
		return MemberRoles.SubjectOf(context.User)!;
	}

	private static IResult Invalid(string detail)
	{
		return Results.Problem(
			title: "That comment cannot be saved.",
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: "https://hpac.ca/problems/invalid-comment");
	}
}
