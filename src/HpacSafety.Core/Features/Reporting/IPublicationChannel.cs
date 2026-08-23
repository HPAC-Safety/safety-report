
using HpacSafety.Core.Features.QuestionBank;
using HpacSafety.Core.SharedKernel;

namespace HpacSafety.Core.Features.Reporting;

/// <summary>An approved summary, in one language, ready to leave the system.</summary>
/// <param name="ReportId">Which report it summarizes.</param>
/// <param name="Locale">The language of this text.</param>
/// <param name="Text">The approved, anonymized summary.</param>
public sealed record PublishableSummary(TinyId ReportId, Locale Locale, string Text);

/// <summary>
/// Somewhere an approved summary goes: the website, and later WhatsApp or
/// Telegram. Declared and deliberately unimplemented in phase 1.
/// </summary>
/// <remarks>
/// This is not an extension point that lets a caller skip the consent gate or
/// human approval. A summary only reaches a channel after
/// <c>Report.IsPublishable</c> is true.
/// </remarks>
public interface IPublicationChannel
{
    /// <summary>The channel's stable name, for audit entries.</summary>
    string Name { get; }

    /// <summary>Publishes one approved summary.</summary>
    Task PublishAsync(PublishableSummary summary, CancellationToken cancellationToken);
}
