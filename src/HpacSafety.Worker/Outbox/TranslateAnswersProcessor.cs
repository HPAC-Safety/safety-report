using HpacSafety.Core;
using HpacSafety.Core.Features.Outbox;
using HpacSafety.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HpacSafety.Worker.Outbox;

/// <summary>
///     Mechanically supplies the second language of every answer on one report,
///     via the same <see cref="ITranslator" /> port question authoring uses. See
///     ADR-0080. Never touches <c>Value</c> or <c>Locale</c> — those are
///     immutable, written once by the submission endpoint.
/// </summary>
public sealed class TranslateAnswersProcessor(HpacSafetyDbContext database, ITranslator translator)
	: IOutboxMessageProcessor
{
	/// <inheritdoc />
	public OutboxMessageType HandlesType => OutboxMessageType.TranslateAnswers;

	/// <inheritdoc />
	public async Task Process(OutboxMessage message,
							  CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var reportId = TinyId.Parse(message.Payload);

		var untranslated = await database.ReportAnswers
			.Where(answer => answer.ReportId == reportId && answer.Value != null && answer.TranslatedValue == null)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		if (untranslated.Count == 0)
		{
			return;
		}

		// Every answer on a report is written in the report's one submitted
		// locale (ADR-0080), so this is one group in practice; grouping stays
		// defensive rather than assumed, and turns N answers into one DeepL
		// call per group instead of N.
		foreach (var group in untranslated.GroupBy(answer => answer.Locale))
		{
			var source = group.Key;
			var target = source.Counterpart;
			var answers = group.ToList();

			var translated = await translator
				.Translate(answers.ConvertAll(answer => answer.Value!), source, target, cancellationToken)
				.ConfigureAwait(false);

			for (var i = 0; i < answers.Count; i++)
			{
				answers[i].SupplyAutoTranslation(translated[i]);
			}
		}
	}
}
