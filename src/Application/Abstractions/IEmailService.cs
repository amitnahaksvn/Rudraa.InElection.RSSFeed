using Domain.Entities;

namespace Application.Abstractions;

/// <summary>
/// Provider-agnostic email notification service. Business logic depends only on this interface,
/// never on a concrete provider SDK type - swapping the implementation (Resend today; SendGrid/SES/
/// SMTP/Azure Communication Services later) never requires touching a caller. Every method is
/// guaranteed to never throw: implementations must catch and log their own failures internally,
/// since a monitoring-alert send failing must never itself take down whatever it was reporting on.
/// Instead, every method returns whether it actually succeeded, so a caller that needs to know
/// (e.g. before marking something as delivered) can react to that without needing a try/catch.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends one email covering a batch of persisted, not-yet-dispatched <see cref="ErrorLog"/>
    /// records - a summary table plus a detailed section per error, each showing its own
    /// <see cref="ErrorLog.Id"/> for lookup in the ErrorLogs collection - so a burst of failures
    /// produces one readable email instead of one per error. Called only by the error-notification
    /// dispatch job on its own schedule, never at the moment an error actually occurs.
    /// </summary>
    /// <returns>True if the email was actually sent; false if it was skipped (disabled/not configured) or the send failed.</returns>
    Task<bool> SendErrorLogBatchAsync(IReadOnlyList<ErrorLog> errors, CancellationToken cancellationToken = default);

    Task<bool> SendWarningAsync(string subject, string message, CancellationToken cancellationToken = default);

    Task<bool> SendInformationAsync(string subject, string message, CancellationToken cancellationToken = default);

    Task<bool> SendSuccessAsync(string subject, string message, CancellationToken cancellationToken = default);
}
