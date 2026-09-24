using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Api.Admin;

/// <summary>
///     The admin report endpoints: the report list, one report's audited detail
///     view, the review commands (save the pair, approve-and-publish, reject,
///     reopen, unpublish), and soft deletion. Every command carries the version the
///     reviewer loaded and is refused with <c>409</c> when it is stale (ADR-0105).
/// </summary>
public static class ReportEndpoints
{
	/// <summary>How long a report may sit in Submitted or Summarizing before it counts as stuck.</summary>
	public static readonly TimeSpan StuckAfter = TimeSpan.FromHours(24);

	/// <summary>Each filter the list accepts, by its query-string code.</summary>
	private static readonly Dictionary<string, Func<ReportListItem, bool>> Filters =
		new(StringComparer.Ordinal)
		{
			["all"] = _ => true,
			["needs-action"] = item => item.IsStuck
				|| item.Status == EnumCode.Of(ReportStatus.PendingReview)
				|| item.Status == EnumCode.Of(ReportStatus.SummaryFailed),
			["published"] = item => item.Status == EnumCode.Of(ReportStatus.Published),
			["private"] = item => item.Consent == "no",
			["rejected"] = item => item.Status == EnumCode.Of(ReportStatus.Rejected),
			["summary-failed"] = item => item.Status == EnumCode.Of(ReportStatus.SummaryFailed),
		};

	/// <summary>Maps the admin report endpoints.</summary>
	/// <param name="app">The route builder.</param>
	/// <returns>The group, so the caller can see what was mapped.</returns>
	public static RouteGroupBuilder MapAdminReports(this IEndpointRouteBuilder app)
	{
		ArgumentNullException.ThrowIfNull(app);

		var group = app.MapGroup("/api/admin/reports").RequireAuthorization(HpacPolicies.Reviewer);

		group.MapGet("/", List);
		group.MapGet("/{id}", Detail);
		group.MapPut("/{id}/summary", SaveSummary);
		group.MapPost("/{id}/approve", Approve);
		group.MapPost("/{id}/reject", Reject);
		group.MapPost("/{id}/reopen", Reopen);
		group.MapPost("/{id}/unpublish", Unpublish);
		group.MapDelete("/{id}", Delete);

		return group;
	}

	/// <summary>
	///     Every live report, newest first, narrowed by <paramref name="filter" />
	///     (REQ-MOD-030, REQ-MOD-049, REQ-MOD-050). State and timing only — the list
	///     carries no answer or summary text, so reading it is not audited.
	/// </summary>
	private static async Task<IResult> List(
		string? filter,
		HpacSafetyDbContext database,
		TimeProvider clock,
		CancellationToken cancellationToken)
	{
		filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter;

		if (!Filters.TryGetValue(filter, out var matches))
		{
			return Results.Problem(
				title: "That filter is not known.",
				detail: $"Use one of: {string.Join(", ", Filters.Keys)}.",
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/unknown-filter");
		}

		var reports = await database.Reports
			.AsNoTracking()
			.OrderByDescending(report => report.SubmittedAt)
			.Select(report => new { report.Id, report.SubmittedAt, report.Status, report.Language, report.ConsentPublish })
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		var now = clock.GetUtcNow();

		return Results.Ok(reports
			.Select(report => new ReportListItem(
				report.Id.Value,
				report.SubmittedAt,
				EnumCode.Of(report.Status),
				report.Language.Code,
				ConsentCode(report.ConsentPublish),
				IsStuck(report.Status, report.SubmittedAt, now)))
			.Where(matches)
			.ToList());
	}

	/// <summary>
	///     One report as a reviewer judges it (REQ-MOD-031). Reading it is a sensitive
	///     read, audited as <see cref="AuditAction.ViewedRawReport" /> in the same save
	///     that precedes the response, so no content leaves without its record
	///     (REQ-MOD-051).
	/// </summary>
	private static async Task<IResult> Detail(
		string id,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		var report = await Load(id, database, cancellationToken).ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();
		database.AuditLog.Add(new AuditLogEntry(SubjectOf(context), AuditAction.ViewedRawReport, "Report", report.Id, at));
		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.Ok(await DetailOf(database, report, at, cancellationToken).ConfigureAwait(false));
	}

	/// <summary>Saves both texts of the pair, or writes it by hand after a failure (REQ-MOD-032, REQ-MOD-059).</summary>
	private static Task<IResult> SaveSummary(string id,
											 SaveSummaryPairRequest request,
											 HpacSafetyDbContext database,
											 TimeProvider clock,
											 HttpContext context,
											 CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, _, at) =>
		{
			if (report.Status == ReportStatus.SummaryFailed)
			{
				report.WriteManualSummary(request.AiSummaryEn ?? string.Empty, request.AiSummaryFr ?? string.Empty, at);
			}
			else
			{
				report.EditSummary(request.AiSummaryEn ?? string.Empty, request.AiSummaryFr ?? string.Empty, at);
			}

			return AuditAction.EditedSummary;
		}, cancellationToken);
	}

	/// <summary>Approves the pair, publishing it when the reporter consented (REQ-MOD-055, ADR-0105).</summary>
	private static Task<IResult> Approve(string id,
										 ReviewCommand request,
										 HpacSafetyDbContext database,
										 TimeProvider clock,
										 HttpContext context,
										 CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, subject, at) =>
		{
			report.ApprovePair(subject, at);
			return AuditAction.ApprovedReport;
		}, cancellationToken);
	}

	/// <summary>Rejects the report, with an optional reviewer-only note (REQ-MOD-034, REQ-MOD-058).</summary>
	private static Task<IResult> Reject(string id,
										RejectReportRequest request,
										HpacSafetyDbContext database,
										TimeProvider clock,
										HttpContext context,
										CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, _, _) =>
		{
			report.RejectReview(request.Note);
			return AuditAction.RejectedReport;
		}, cancellationToken);
	}

	/// <summary>Returns a rejected report to review (REQ-MOD-056).</summary>
	private static Task<IResult> Reopen(string id,
										ReviewCommand request,
										HpacSafetyDbContext database,
										TimeProvider clock,
										HttpContext context,
										CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, _, _) =>
		{
			report.Reopen();
			return AuditAction.ReopenedReport;
		}, cancellationToken);
	}

	/// <summary>Takes a published report off the public feed and back to review (REQ-MOD-057).</summary>
	private static Task<IResult> Unpublish(string id,
										   ReviewCommand request,
										   HpacSafetyDbContext database,
										   TimeProvider clock,
										   HttpContext context,
										   CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, _, _) =>
		{
			report.Unpublish();
			return AuditAction.UnpublishedReport;
		}, cancellationToken);
	}

	/// <summary>
	///     Runs one review command: refuses a stale version with <c>409</c>, applies the
	///     domain command, and saves it with one content-free audit entry in the same
	///     transaction (REQ-MOD-060, REQ-MOD-061). Answers with the updated detail view,
	///     carrying its new version; that response is not a second audited read.
	/// </summary>
	private static async Task<IResult> Act(string id,
										   string? version,
										   HpacSafetyDbContext database,
										   TimeProvider clock,
										   HttpContext context,
										   Func<Report, string, DateTimeOffset, AuditAction> command,
										   CancellationToken cancellationToken)
	{
		var report = await Load(id, database, cancellationToken).ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		if (version != ConcurrencyToken.Of(database, report) || !ConcurrencyToken.Expect(database, report, version))
		{
			return Stale();
		}

		var at = clock.GetUtcNow();
		var subject = SubjectOf(context);
		AuditAction action;

		try
		{
			action = command(report, subject, at);
		}
		catch (ReviewTransitionException refusal)
		{
			return Results.Problem(
				title: "That action is not allowed now.",
				detail: refusal.Message,
				statusCode: StatusCodes.Status409Conflict,
				type: "https://hpac.ca/problems/invalid-transition");
		}
		catch (DomainRuleViolationException refusal)
		{
			return Results.Problem(
				title: "That change cannot be saved.",
				detail: refusal.Message,
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/invalid-review");
		}

		database.AuditLog.Add(new AuditLogEntry(subject, action, "Report", report.Id, at));

		try
		{
			await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (DbUpdateConcurrencyException)
		{
			return Stale();
		}

		return Results.Ok(await DetailOf(database, report, at, cancellationToken).ConfigureAwait(false));
	}

	private static IResult Stale()
	{
		return Results.Problem(
			title: "This report changed since you opened it.",
			detail: "Another reviewer saved a change to this report. Reload it to see the latest version, then try again.",
			statusCode: StatusCodes.Status409Conflict,
			type: "https://hpac.ca/problems/stale-report");
	}

	private static string SubjectOf(HttpContext context)
	{
		// A validated token should always carry a subject, but /me already treats
		// that as unguaranteed; the audit row records it as unknown rather than failing.
		return MemberRoles.SubjectOf(context.User) ?? "(unknown)";
	}

	private static async Task<Report?> Load(string id,
											HpacSafetyDbContext database,
											CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var reportId))
		{
			return null;
		}

		return await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
			.ConfigureAwait(false);
	}

	private static async Task<ReportDetail> DetailOf(HpacSafetyDbContext database,
													 Report report,
													 DateTimeOffset at,
													 CancellationToken cancellationToken)
	{
		// The exact revisions answered, retired ones included: an answer always
		// shows the wording the reporter actually saw (ADR-0071).
		var revisionIds = report.Answers.Select(answer => answer.QuestionRevisionId).Distinct().ToList();
		var revisions = await database.QuestionRevisions
			.IgnoreQueryFilters()
			.AsNoTracking()
			.Where(revision => revisionIds.Contains(revision.Id))
			.ToDictionaryAsync(revision => revision.Id, cancellationToken)
			.ConfigureAwait(false);

		return new ReportDetail(
			report.Id.Value,
			report.SubmittedAt,
			EnumCode.Of(report.Status),
			report.Language.Code,
			ConsentCode(report.ConsentPublish),
			IsStuck(report.Status, report.SubmittedAt, at),
			report.SummaryError,
			AnswersOf(report, revisions),
			report.Summary is { } summary
				? new ReportSummaryView(
					summary.AiSummaryEn,
					summary.AiSummaryFr,
					summary.Model,
					summary.PromptVersion,
					summary.GeneratedAt,
					summary.UpdatedAt,
					summary.ApprovedBySubject,
					summary.ApprovedAt)
				: null,
			[.. report.Files.Select(file => new ReportAttachmentView(file.Id.Value, EnumCode.Of(file.Kind), AttachmentState(file)))],
			ConcurrencyToken.Of(database, report),
			report.RejectionNote,
			report.PublishedAt);
	}

	/// <summary>Groups answers by question, in form order, under the revision each was given against.</summary>
	private static List<ReportAnswerView> AnswersOf(Report report,
													Dictionary<TinyId, QuestionRevision> revisions)
	{
		return report.Answers
			.Where(answer => revisions.ContainsKey(answer.QuestionRevisionId))
			.GroupBy(answer => answer.QuestionRevisionId)
			.Select(group => (Revision: revisions[group.Key], Answers: group.ToList()))
			.OrderBy(entry => entry.Revision.DisplayOrder)
			.ThenBy(entry => entry.Answers[0].QuestionKey, StringComparer.Ordinal)
			.Select(entry => new ReportAnswerView(
				entry.Answers[0].QuestionKey,
				entry.Revision.LabelEn,
				entry.Revision.LabelFr,
				entry.Answers[0].IsPrivate,
				[
					.. entry.Answers
						.Where(answer => answer.Value is not null)
						.Select(answer => new ReportAnswerValueView(
							answer.Value!,
							answer.Locale.Code,
							answer.TranslatedValue,
							answer.TranslationSource is { } source ? EnumCode.Of(source) : null)),
				]))
			.ToList();
	}

	private static string AttachmentState(ReportFile file)
	{
		if (file.ProcessingErrorCode is not null)
		{
			return "failed";
		}

		// A document is served as its validated original; only images and videos
		// wait for a stripped derivative (REQ-MED-013).
		return file.Kind is AttachmentKind.Document || !file.AwaitsStripping ? "ready" : "processing";
	}

	private static string ConsentCode(bool? consent)
	{
		return consent switch
		{
			true => "yes",
			false => "no",
			null => "unanswered",
		};
	}

	private static bool IsStuck(ReportStatus status,
								DateTimeOffset submittedAt,
								DateTimeOffset now)
	{
		return status is ReportStatus.Submitted or ReportStatus.Summarizing
			&& now - submittedAt > StuckAfter;
	}

	/// <summary>
	///     Soft-deletes a report and everything it owns — answers, files, summary,
	///     and pending outbox work — with one shared timestamp, in one transaction.
	///     Irreversible: there is no restore endpoint (REQ-DOM-007).
	/// </summary>
	private static async Task<IResult> Delete(
		string id,
		HpacSafetyDbContext database,
		TimeProvider clock,
		HttpContext context,
		CancellationToken cancellationToken)
	{
		if (!TinyId.TryParse(id, out var reportId))
		{
			return Results.NotFound();
		}

		var report = await database.Reports
			.Include(candidate => candidate.Answers)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
			.ConfigureAwait(false);

		if (report is null)
		{
			return Results.NotFound();
		}

		var at = clock.GetUtcNow();

		report.SoftDelete(at);

		var pendingOutbox = await database.OutboxMessages
			.Where(message => message.AggregateId == reportId)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		foreach (var message in pendingOutbox)
		{
			message.Delete(at);
		}

		// A validated token should always carry a subject, but the /me endpoint
		// already treats that as unguaranteed rather than assumed — a role claim
		// alone does not prove a subject claim exists. Same stance here.
		var subject = SubjectOf(context);
		database.AuditLog.Add(new AuditLogEntry(subject, AuditAction.DeletedReport, "Report", reportId, at));

		await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		return Results.NoContent();
	}
}
