using Mediator;
using Application.Abstractions;

namespace Application.ErrorLogs.Commands.SetErrorLogResolved;

/// <summary>Marks (or un-marks) one error as resolved from the error-monitor UI, with a required comment recorded in its history. Returns false when no row with that id exists, so the endpoint can 404 instead of silently no-op'ing.</summary>
public sealed record SetErrorLogResolvedCommand(string Id, bool Resolved, string Comment) : IRequest<bool>;

public sealed class SetErrorLogResolvedCommandHandler : IRequestHandler<SetErrorLogResolvedCommand, bool>
{
    private readonly IErrorLogRepository _errorLogs;

    public SetErrorLogResolvedCommandHandler(IErrorLogRepository errorLogs)
    {
        _errorLogs = errorLogs;
    }

    public async ValueTask<bool> Handle(SetErrorLogResolvedCommand request, CancellationToken cancellationToken) =>
        await _errorLogs.SetResolvedAsync(request.Id, request.Resolved, request.Comment, cancellationToken);
}
