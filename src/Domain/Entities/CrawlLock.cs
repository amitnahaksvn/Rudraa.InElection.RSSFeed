namespace Domain.Entities;

/// <summary>
/// A distributed mutual-exclusion lock stored in Mongo so that only one crawler
/// instance (across processes/machines) runs a given lock name at a time.
/// </summary>
public sealed class CrawlLock
{
    /// <summary>The lock name, e.g. "news-crawler". Doubles as the Mongo document id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Opaque identifier of the process/instance currently holding the lock.</summary>
    public string Owner { get; set; } = string.Empty;

    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>Lock auto-expires at this time so a crashed owner cannot block future runs forever.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
