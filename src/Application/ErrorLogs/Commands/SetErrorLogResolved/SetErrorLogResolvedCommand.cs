using Mediator;
using Application.Abstractions;

namespace Application.ErrorLogs.Commands.SetErrorLogResolved;

/// <summary>Marks (or un-marks) one error as resolved from the error-monitor UI, with a required comment (and optional longer-form description) recorded in its history. Returns false when no row with that id exists, so the endpoint can 404 instead of silently no-op'ing.</summary>
public sealed record SetErrorLogResolvedCommand(
    string Id,
    bool Resolved,
    string Comment,
    string? Description = null,
    string? OriginClientId = null) : IRequest<bool>;

public sealed class SetErrorLogResolvedCommandHandler : IRequestHandler<SetErrorLogResolvedCommand, bool>
{
    private readonly IErrorLogRepository _errorLogs;
    private readonly IErrorLogNotifier _notifier;

    public SetErrorLogResolvedCommandHandler(IErrorLogRepository errorLogs, IErrorLogNotifier notifier)
    {
        _errorLogs = errorLogs;
        _notifier = notifier;
    }

    public async ValueTask<bool> Handle(SetErrorLogResolvedCommand request, CancellationToken cancellationToken)
    {
        var found = await _errorLogs.SetResolvedAsync(request.Id, request.Resolved, request.Comment, request.Description, cancellationToken);
        if (found)
        {
            var now = DateTimeOffset.UtcNow;
            await _notifier.NotifyResolvedChangedAsync(
                request.Id,
                request.Resolved,
                request.Resolved ? now : null,
                request.Comment,
                request.Description,
                now,
                request.OriginClientId,
                cancellationToken);
        }

        return found;
    }
}
