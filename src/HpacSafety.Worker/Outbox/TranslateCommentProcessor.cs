using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     Machine-translates one revision of a member's comment into the other
///     official language, through the same <see cref="ITranslator" /> port answers
///     use (ADR-0114). Never touches the text as written. A revision already
///     translated, or deleted since it was queued, needs nothing.
/// </summary>
public sealed class TranslateCommentProcessor(HpacSafetyDbContext database, ITranslator translator)
	: IOutboxMessageProcessor
{
	/// <inheritdoc />
	public OutboxMessageType HandlesType => OutboxMessageType.TranslateComment;

	/// <inheritdoc />
	public async Task Process(OutboxMessage message,
							  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var revisionId = TinyId.Parse(message.Payload);

		var revision = await database.ReportCommentRevisions
			.SingleOrDefaultAsync(candidate => candidate.Id == revisionId, cancellationToken)
			.ConfigureAwait(false);

		if (revision is null || revision.TranslatedText is not null)
		{
			return;
		}

		var translated = await translator
			.Translate([revision.Text], revision.Locale, revision.Locale.Counterpart, cancellationToken)
			.ConfigureAwait(false);

		revision.SupplyAutoTranslation(translated[0]);
	}
}
