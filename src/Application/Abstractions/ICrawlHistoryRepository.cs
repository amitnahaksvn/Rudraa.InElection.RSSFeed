using Domain.Entities;

namespace Application.Abstractions;

public interface ICrawlHistoryRepository
{
    /// <summary>Inserts a new history record (status = Running) and returns its generated Id.</summary>
    Task<string> InsertAsync(CrawlHistory history, CancellationToken cancellationToken);

    Task UpdateAsync(CrawlHistory history, CancellationToken cancellationToken);

    Task<IReadOnlyList<CrawlHistory>> GetRecentAsync(int count, CancellationToken cancellationToken);

    /// <returns>Null if no crawl history record with that id exists.</returns>
    Task<CrawlHistory?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
