using Application.Abstractions;

namespace WebPlatform;

/// <summary>
/// No-op <see cref="IErrorLogNotifier"/> for RssService/ApiService - they never serve the
/// error-monitor SPA (only WebApp does, via <c>WebPlatform.Hubs.SignalRErrorLogNotifier</c>), but
/// the error-log mutation command handlers (resolve/comment) still take an
/// <see cref="IErrorLogNotifier"/> unconditionally, and ASP.NET Core's Development-time DI
/// validation (<c>ValidateOnBuild</c>) fails startup if nothing is registered for it - even
/// though neither host ever actually invokes those handlers, since it has no ErrorLogs endpoints
/// mapped at all.
/// </summary>
public sealed class NullErrorLogNotifier : IErrorLogNotifier
{
    public Task NotifyResolvedChangedAsync(
        string id,
        bool resolved,
        DateTimeOffset? resolvedOn,
        string comment,
        string? description,
        DateTimeOffset createdOn,
        string? originClientId,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyCommentAddedAsync(
        string id,
        string comment,
        string? description,
        DateTimeOffset createdOn,
        string? originClientId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
