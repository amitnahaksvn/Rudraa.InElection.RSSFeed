using Application.Models;

namespace Application.Abstractions;

public interface ICrawlJobStatusReader
{
    /// <returns>Null if no recurring job is registered for that provider.</returns>
    CrawlJobStatus? GetStatus(string providerName);
}
