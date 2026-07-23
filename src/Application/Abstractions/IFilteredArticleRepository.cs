using Domain.Entities;

namespace Application.Abstractions;

/// <summary>Persistence for <see cref="FilteredArticle"/> - the log of articles excluded by the political-category allowlist (see <c>Application.Options.NewsFilterOptions</c>).</summary>
public interface IFilteredArticleRepository
{
    Task InsertAsync(FilteredArticle article, CancellationToken cancellationToken);

    /// <summary>Newest-first page of filtered rows.</summary>
    Task<IReadOnlyList<FilteredArticle>> GetPagedAsync(int skip, int limit, CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <summary>Returns false when no row with that id exists.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
