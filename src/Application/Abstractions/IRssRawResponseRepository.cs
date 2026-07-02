using Domain.Entities;

namespace Application.Abstractions;

/// <summary>
/// Archives every RSS fetch exactly as received, regardless of whether parsing succeeded.
/// Retention is enforced two ways: passively by a Mongo TTL index (self-healing background sweep,
/// see <see cref="Options.NewsCrawlerOptions.RawResponseRetention"/>) and actively by
/// <see cref="DeleteOlderThanAsync"/> on a daily schedule (see
/// <c>Infrastructure/Scheduling/HangfireRawResponseCleanupExecutor</c>) for predictable, observable
/// timing rather than waiting on Mongo's own sweep interval.
/// </summary>
public interface IRssRawResponseRepository
{
    Task InsertAsync(RssRawResponse response, CancellationToken cancellationToken);

    Task<IReadOnlyList<RssRawResponse>> GetRecentAsync(
        string provider, string feedName, int count, CancellationToken cancellationToken);

    /// <returns>The number of documents deleted.</returns>
    Task<long> DeleteOlderThanAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(TimeSpan retention, CancellationToken cancellationToken);
}
