
namespace HpacSafety.Core.SharedKernel;

/// <summary>
/// One message to send. The body carries a link, never the report — an inbox is
/// outside this system's access controls.
/// </summary>
/// <param name="To">The recipient address.</param>
/// <param name="Subject">Already localized, from <c>locales/</c>.</param>
/// <param name="Body">Already localized, from <c>locales/</c>.</param>
/// <param name="Locale">The locale it was localized to, for logging and tests.</param>
public sealed record EmailMessage(string To, string Subject, string Body, Locale Locale);

/// <summary>Sends mail. Failure here must never roll back a report submission,
/// which is why notification rides the outbox.</summary>
public interface IEmailSender
{
    /// <summary>Sends one already-localized message.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
