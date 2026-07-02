namespace Application.Abstractions;

/// <summary>
/// Distributed mutual-exclusion lock backed by Mongo, so only one crawler instance
/// (across processes/machines) executes a crawl run at a time.
/// </summary>
public interface ICrawlLockRepository
{
    /// <summary>
    /// Attempts to atomically acquire <paramref name="lockName"/> for <paramref name="owner"/>.
    /// Succeeds if the lock is unheld or its previous holder's lease has expired.
    /// </summary>
    Task<bool> TryAcquireAsync(string lockName, string owner, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Releases the lock, but only if still held by <paramref name="owner"/>.</summary>
    Task ReleaseAsync(string lockName, string owner, CancellationToken cancellationToken);

    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
