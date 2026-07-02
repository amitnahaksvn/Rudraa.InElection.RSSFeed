namespace Application.Abstractions;

/// <summary>Deletes archived raw RSS responses older than a given age.</summary>
public interface IRawResponseCleanupService
{
    /// <returns>The number of documents deleted.</returns>
    Task<long> CleanupAsync(TimeSpan retention, CancellationToken cancellationToken);
}
