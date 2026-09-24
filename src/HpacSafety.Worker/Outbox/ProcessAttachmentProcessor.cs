using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Core.Features.Reporting;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     Produces one attachment's reviewer-safe derivative, off the submission path:
///     the submission copied the original into the report's compartment and
///     enqueued this, one message per file (ADR-0098, REQ-MED-009).
/// </summary>
/// <remarks>
///     Reads current state rather than trusting the message (CON-IF-008). A file
///     whose report was deleted, that already has a derivative, or that already
///     failed is left alone, so a redelivered message changes nothing. A file the
///     image library cannot clean, or whose bytes no longer match what it was
///     recorded as, is marked failed with a safe code and never becomes viewable
///     (REQ-MED-013). A storage or database error is not caught: the message is
///     retried with backoff.
/// </remarks>
public sealed class ProcessAttachmentProcessor(HpacSafetyDbContext database, MediaIngestor ingestor, TimeProvider clock)
	: IOutboxMessageProcessor
{
	/// <inheritdoc />
	public OutboxMessageType HandlesType => OutboxMessageType.ProcessAttachment;

	/// <inheritdoc />
	public async Task Process(OutboxMessage message,
							  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var fileId = TinyId.Parse(message.Payload);

		// Deleted files are filtered out by default; they are read here only to
		// be recognised and skipped.
		var file = await database.ReportFiles
			.IgnoreQueryFilters()
			.SingleOrDefaultAsync(candidate => candidate.Id == fileId, cancellationToken)
			.ConfigureAwait(false);

		if (file is null
			|| file.Deleted is not null
			|| file.ProcessingErrorCode is not null
			|| !file.AwaitsStripping
			|| file.ValidatedAt is not null)
		{
			return;
		}

		if (!MediaType.TryParse(file.ContentType, out var recorded))
		{
			file.RecordProcessingFailure(EnumCode.Of(MediaRejectionReason.UnacceptedMediaType));
			return;
		}

		var outcome = await ingestor
			.Process(BlobKey.Parse(file.BlobKey), recorded, cancellationToken)
			.ConfigureAwait(false);

		if (!outcome.IsAccepted)
		{
			file.RecordProcessingFailure(EnumCode.Of(outcome.RejectionReason));
			return;
		}

		if (outcome.IsViewable)
		{
			file.RecordStripped(outcome.DerivativeKey.Value, outcome.StrippedAt!.Value);
		}

		else if (file.Kind is AttachmentKind.Document)
		{
			// A document has no derivative; this is what says it passed (ADR-0119).
			file.RecordValidated(clock.GetUtcNow());
		}

		// Otherwise a video retained with no derivative because it could not be
		// remuxed (ADR-0094). Nothing more to record.
	}
}
