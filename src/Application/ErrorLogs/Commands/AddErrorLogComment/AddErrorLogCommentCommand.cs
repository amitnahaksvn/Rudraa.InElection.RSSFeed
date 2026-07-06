using Mediator;
using Application.Abstractions;

namespace Application.ErrorLogs.Commands.AddErrorLogComment;

/// <summary>Appends a standalone comment to an error's history without changing its resolved status. Returns false when no row with that id exists.</summary>
public sealed record AddErrorLogCommentCommand(string Id, string Comment) : IRequest<bool>;

public sealed class AddErrorLogCommentCommandHandler : IRequestHandler<AddErrorLogCommentCommand, bool>
{
    private readonly IErrorLogRepository _errorLogs;

    public AddErrorLogCommentCommandHandler(IErrorLogRepository errorLogs)
    {
        _errorLogs = errorLogs;
    }

    public async ValueTask<bool> Handle(AddErrorLogCommentCommand request, CancellationToken cancellationToken) =>
        await _errorLogs.AddCommentAsync(request.Id, request.Comment, cancellationToken);
}
