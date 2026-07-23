using Mediator;
using Application.Abstractions;

namespace Application.FilteredArticles.Commands.DeleteFilteredArticle;

/// <summary>Deletes one <see cref="Domain.Entities.FilteredArticle"/> row by id - backs the admin page's per-row delete button. Unlike <c>DeleteArticlesCommand</c>, this is a hard delete (the row is just a low-value diagnostic log, not a business record worth soft-preserving).</summary>
public sealed record DeleteFilteredArticleCommand(string Id) : IRequest<bool>;

public sealed class DeleteFilteredArticleCommandHandler : IRequestHandler<DeleteFilteredArticleCommand, bool>
{
    private readonly IFilteredArticleRepository _filteredArticles;

    public DeleteFilteredArticleCommandHandler(IFilteredArticleRepository filteredArticles)
    {
        _filteredArticles = filteredArticles;
    }

    public async ValueTask<bool> Handle(DeleteFilteredArticleCommand request, CancellationToken cancellationToken) =>
        await _filteredArticles.DeleteAsync(request.Id, cancellationToken);
}
