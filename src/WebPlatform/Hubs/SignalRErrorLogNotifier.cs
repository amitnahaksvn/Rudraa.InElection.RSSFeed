using Microsoft.AspNetCore.SignalR;
using Application.Abstractions;

namespace WebPlatform.Hubs;

/// <summary>
/// Broadcasts to every connected error-monitor client - deliberately <c>Clients.All</c> rather
/// than per-error groups, since this is a small internal admin dashboard, not a large
/// multi-tenant surface; each client decides for itself (by matching the error id against what it
/// has cached/open, and ignoring its own <c>originClientId</c>) whether an update is relevant.
/// </summary>
public sealed class SignalRErrorLogNotifier : IErrorLogNotifier
{
    private readonly IHubContext<ErrorLogHub> _hub;

    public SignalRErrorLogNotifier(IHubContext<ErrorLogHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyResolvedChangedAsync(
        string id,
        bool resolved,
        DateTimeOffset? resolvedOn,
        string comment,
        string? description,
        DateTimeOffset createdOn,
        string? originClientId,
        CancellationToken cancellationToken) =>
        _hub.Clients.All.SendAsync(
            "errorResolvedChanged",
            new { id, resolved, resolvedOn, comment, description, createdOn, originClientId },
            cancellationToken);

    public Task NotifyCommentAddedAsync(
        string id,
        string comment,
        string? description,
        DateTimeOffset createdOn,
        string? originClientId,
        CancellationToken cancellationToken) =>
        _hub.Clients.All.SendAsync(
            "errorCommentAdded",
            new { id, comment, description, createdOn, originClientId },
            cancellationToken);
}
