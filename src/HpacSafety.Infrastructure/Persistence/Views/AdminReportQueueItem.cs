using HpacSafety.Core;
using HpacSafety.Core.Features.Reporting;

namespace HpacSafety.Infrastructure.Persistence.Views;

/// <summary>
///     One row of the <c>admin_report_queue</c> view: a live report as the admin
///     list shows it, state and timing only (REQ-MOD-030). The view, not this type,
///     decides what counts as stuck and what needs action (REQ-MOD-049).
/// </summary>
public sealed class AdminReportQueueItem
{
	/// <summary>The report.</summary>
	public TinyId Id { get; private init; }

	/// <summary>When it was submitted.</summary>
	public DateTimeOffset SubmittedAt { get; private init; }

	/// <summary>Its workflow status.</summary>
	public ReportStatus Status { get; private init; }

	/// <summary>The language it was filed in.</summary>
	public Locale Language { get; private init; }

	/// <summary>Publication consent: yes, no, or unanswered on an older report.</summary>
	public bool? ConsentPublish { get; private init; }

	/// <summary>Whether it has waited in Submitted or Summarizing for more than 24 hours.</summary>
	public bool IsStuck { get; private init; }

	/// <summary>Whether a reviewer has something to do: pending review, summary failed, or stuck.</summary>
	public bool NeedsAction { get; private init; }

	/// <summary>
	///     The version a review command sends back, the same value the detail view
	///     gives (<see cref="ConcurrencyToken.Of" />), so a row action needs no
	///     audited detail read (ADR-0105).
	/// </summary>
	public string Version { get; private init; } = string.Empty;

	/// <summary>
	///     The reporter's first and last name answers, joined, or null when
	///     neither was given. Read by <c>QuestionRole</c>, not by wording or
	///     position, so it survives a fork (ADR-0154). The one exception, with
	///     <see cref="PilotName" />, to this list carrying no other answer text
	///     (REQ-MOD-030, REQ-MOD-124).
	/// </summary>
	public string? ReporterName { get; private init; }

	/// <summary>The pilot's first and last name answers, joined, or null when neither was given (ADR-0154).</summary>
	public string? PilotName { get; private init; }
}
