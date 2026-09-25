using System.Linq.Expressions;
using HpacSafety.Api.Authentication;
using HpacSafety.Core;
using HpacSafety.Core.Features.Moderation;
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using HpacSafety.Infrastructure.Persistence.Views;
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
	/// <summary>
	///     Each filter the list accepts, by its query-string code. Stuck and needs
	///     action are the <c>admin_report_queue</c> view's rule, not this file's.
	/// </summary>
	private static readonly Dictionary<string, Expression<Func<AdminReportQueueItem, bool>>> Filters =
		new(StringComparer.Ordinal)
		{
			["all"] = _ => true,
			["needs-action"] = item => item.NeedsAction,
			["published"] = item => item.Status == ReportStatus.Published,
			["private"] = item => item.ConsentPublish == false,
			["unpublished"] = item => item.Status == ReportStatus.Unpublished,
			["summary-failed"] = item => item.Status == ReportStatus.SummaryFailed,
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
		group.MapPost("/{id}/publish", Publish);
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

		var reports = await database.AdminReportQueue
			.AsNoTracking()
			.Where(matches)
			.OrderByDescending(report => report.SubmittedAt)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return Results.Ok(reports
			.Select(report => new ReportListItem(
				report.Id.Value,
				report.SubmittedAt,
				EnumCode.Of(report.Status),
				report.Language.Code,
				ConsentCode(report.ConsentPublish),
				report.IsStuck))
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

		return Results.Ok(await DetailOf(database, report, cancellationToken).ConfigureAwait(false));
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

		if (!TryReadSource(request.SourceEn, out var sourceEn) || !TryReadSource(request.SourceFr, out var sourceFr))
		{
			return Task.FromResult(Results.Problem(
				title: "That change cannot be saved.",
				detail: "A summary language is saved as human or machine.",
				statusCode: StatusCodes.Status400BadRequest,
				type: "https://hpac.ca/problems/invalid-review"));
		}

		return Act(id, request.Version, database, clock, context, (report, _, at) =>
		{
			if (report.Status == ReportStatus.SummaryFailed)
			{
				report.WriteManualSummary(request.AiSummaryEn ?? string.Empty, request.AiSummaryFr ?? string.Empty, at, sourceEn, sourceFr);
			}
			else
			{
				report.EditSummary(request.AiSummaryEn ?? string.Empty, request.AiSummaryFr ?? string.Empty, at, sourceEn, sourceFr);
			}

			return AuditAction.EditedSummary;
		}, cancellationToken);
	}

	/// <summary>A reviewer's language is <c>human</c> unless they say it is an accepted translation.</summary>
	private static bool TryReadSource(string? code,
									  out SummaryTextSource source)
	{
		source = SummaryTextSource.Human;

		if (string.IsNullOrWhiteSpace(code))
		{
			return true;
		}

		return EnumCode.TryParse(code, out source) && source is SummaryTextSource.Human or SummaryTextSource.Machine;
	}

	/// <summary>Approves the pair and publishes the report in one action (REQ-MOD-055, ADR-0125).</summary>
	private static Task<IResult> Publish(string id,
										 ReviewCommand request,
										 HpacSafetyDbContext database,
										 TimeProvider clock,
										 HttpContext context,
										 CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, subject, at) =>
		{
			report.Publish(subject, at);
			return AuditAction.PublishedReport;
		}, cancellationToken);
	}

	/// <summary>
	///     Takes a report off the public feed, or declines a pending one, with an
	///     optional reviewer-only note (REQ-MOD-057, REQ-MOD-058).
	/// </summary>
	private static Task<IResult> Unpublish(string id,
										   UnpublishReportRequest request,
										   HpacSafetyDbContext database,
										   TimeProvider clock,
										   HttpContext context,
										   CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Act(id, request.Version, database, clock, context, (report, _, _) =>
		{
			report.Unpublish(request.Note);
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

		return Results.Ok(await DetailOf(database, report, cancellationToken).ConfigureAwait(false));
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
			.Include(candidate => candidate.Answers).ThenInclude(answer => answer.Choice)
			.Include(candidate => candidate.Files)
			.Include(candidate => candidate.Summary)
			.SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
			.ConfigureAwait(false);
	}

	private static async Task<ReportDetail> DetailOf(HpacSafetyDbContext database,
													 Report report,
													 CancellationToken cancellationToken)
	{
		var isStuck = await database.AdminReportQueue
			.Where(item => item.Id == report.Id)
			.Select(item => item.IsStuck)
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

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
			ConsentCode(report.ConsentMedia),
			isStuck,
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
					summary.ApprovedAt,
					EnumCode.Of(summary.SourceEn),
					EnumCode.Of(summary.SourceFr))
				: null,
			[.. report.Files.Select(file => new ReportAttachmentView(file.Id.Value, EnumCode.Of(file.Kind), AttachmentState(file), Visibility(report, file)))],
			ConcurrencyToken.Of(database, report),
			report.UnpublishNote,
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
				EnumCode.Of(entry.Revision.Type),
				entry.Answers[0].IsPrivate,
				[
					.. entry.Answers
						.Where(answer => answer.IsAnswered)
						.Select(answer => new ReportAnswerValueView(
							(object?)answer.BooleanValue ?? answer.Text!,
							answer.Locale.Code,
							answer.DisplayedTranslation,
							answer.DisplayedTranslation is not null && answer.TranslationSource is { } source
								? EnumCode.Of(source)
								: null)),
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

	/// <summary>
	///     Whether the published report shows this file, by the same rule the
	///     <c>public_report_media</c> view holds (ADR-0117, ADR-0119), so a
	///     reviewer sees what a visitor would.
	/// </summary>
	private static string Visibility(Report report,
									 ReportFile file)
	{
		var isDocument = file.Kind is AttachmentKind.Document;

		if (file.ProcessingErrorCode is not null
			|| (isDocument ? file.ValidatedAt is null : file.AwaitsStripping))
		{
			return "private";
		}

		if (file.HiddenAt is not null)
		{
			return "hidden";
		}

		if ((isDocument ? report.ConsentDocuments : report.ConsentMedia) is not true)
		{
			return "no_consent";
		}

		return report.Status is ReportStatus.Published ? "public" : "when_published";
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
