namespace Domain.Enums;

/// <summary>
/// Outcome of a single crawl run recorded in <see cref="Entities.CrawlHistory"/>.
/// </summary>
public enum CrawlStatus
{
    Running,
    Completed,
    CompletedWithErrors,
    Failed,

    /// <summary>Not run - another crawl (scheduled or manually triggered) was already in progress.</summary>
    Skipped
}
