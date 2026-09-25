using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     Supplies the missing language of one type-ahead value a reporter added,
///     through the same <see cref="ITranslator" /> port answers use, and records it
///     as machine-translated (ADR-0129). The reporter's own wording is never
///     touched, and the value's review state is not this processor's to change. A
///     value that has both languages by now — a reviewer got there first — needs
///     nothing.
/// </summary>
public sealed class TranslateChoiceProcessor(HpacSafetyDbContext database, ITranslator translator)
	: IOutboxMessageProcessor
{
	/// <inheritdoc />
	public OutboxMessageType HandlesType => OutboxMessageType.TranslateChoice;

	/// <inheritdoc />
	public async Task Process(OutboxMessage message,
							  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var choiceId = TinyId.Parse(message.Payload);

		var choice = await database.QuestionChoices
			.SingleOrDefaultAsync(candidate => candidate.Id == choiceId, cancellationToken)
			.ConfigureAwait(false);

		if (choice is not { NeedsTranslation: true })
		{
			return;
		}

		var source = choice.LabelEn is null ? Locale.FrCa : Locale.EnCa;
		var translated = await translator
			.Translate([choice.Label(source)], source, source.Counterpart, cancellationToken)
			.ConfigureAwait(false);

		choice.SupplyAutoTranslation(translated[0]);
	}
}
