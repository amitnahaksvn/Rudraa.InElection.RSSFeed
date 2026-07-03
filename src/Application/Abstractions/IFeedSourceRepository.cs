using Domain.Entities;

namespace Application.Abstractions;

/// <summary>
/// Persistence for <see cref="FeedSource"/> documents - the Mongo-driven feed list read by
/// <c>DynamicFeedIngestionService</c>/the startup recurring-job registration, as opposed
/// to the file-based <c>NewsCrawler.appsettings.json</c> provider list.
/// </summary>
public interface IFeedSourceRepository
{
    Task<IReadOnlyList<FeedSource>> GetActiveAsync(CancellationToken cancellationToken);

    Task<FeedSource?> GetBySourceCodeAsync(string sourceCode, CancellationToken cancellationToken);

    Task<FeedSource?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>Inserts a new document and returns its generated Id.</summary>
    Task<string> InsertAsync(FeedSource feedSource, CancellationToken cancellationToken);

    Task UpdateLastFetchedOnAsync(string id, DateTimeOffset lastFetchedOn, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
