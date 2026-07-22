namespace Application.Abstractions;

/// <summary>Batches every pending (not-yet-emailed) <c>ErrorLog</c> row into one summary email and marks them sent.</summary>
public interface IErrorNotificationDispatchService
{
    /// <returns>The number of error logs included in the dispatched email (0 if nothing was pending, or if the email failed to send).</returns>
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
