using Mediator;
using Application.Abstractions;

namespace Application.ErrorLogs.Commands.AddErrorLogComment;

/// <summary>Appends a standalone comment (with an optional longer-form description) to an error's history without changing its resolved status. Returns false when no row with that id exists.</summary>
public sealed record AddErrorLogCommentCommand(
    string Id,
    string Comment,
    string? Description = null,
    string? OriginClientId = null) : IRequest<bool>;

public sealed class AddErrorLogCommentCommandHandler : IRequestHandler<AddErrorLogCommentCommand, bool>
{
    private readonly IErrorLogRepository _errorLogs;
    private readonly IErrorLogNotifier _notifier;

    public AddErrorLogCommentCommandHandler(IErrorLogRepository errorLogs, IErrorLogNotifier notifier)
    {
        _errorLogs = errorLogs;
        _notifier = notifier;
    }

    public async ValueTask<bool> Handle(AddErrorLogCommentCommand request, CancellationToken cancellationToken)
    {
        var found = await _errorLogs.AddCommentAsync(request.Id, request.Comment, request.Description, cancellationToken);
        if (found)
        {
            await _notifier.NotifyCommentAddedAsync(
                request.Id,
                request.Comment,
                request.Description,
                DateTimeOffset.UtcNow,
                request.OriginClientId,
                cancellationToken);
        }

        return found;
    }
}
