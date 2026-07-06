namespace Application.Abstractions;

/// <summary>
/// Pushes a live update to any connected error-monitor UI when an error's resolved status
/// changes or a comment is added - implemented in Web (the only host with an HTTP/real-time
/// pipeline) via SignalR, called from the command handlers only after the repository mutation
/// has actually succeeded. <paramref name="originClientId"/> is the browser tab that made the
/// change (see the X-ErrorLog-Client-Id header) - the frontend uses it to skip re-applying an
/// update it already applied optimistically from its own mutation's onSuccess.
/// </summary>
public interface IErrorLogNotifier
{
    Task NotifyResolvedChangedAsync(
        string id,
        bool resolved,
        DateTimeOffset? resolvedOn,
        string comment,
        string? description,
        DateTimeOffset createdOn,
        string? originClientId,
        CancellationToken cancellationToken);

    Task NotifyCommentAddedAsync(
        string id,
        string comment,
        string? description,
        DateTimeOffset createdOn,
        string? originClientId,
        CancellationToken cancellationToken);
}
