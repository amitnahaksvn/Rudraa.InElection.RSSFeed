using Application.Models;
using Application.Options;

namespace Application.Abstractions;

/// <summary>
/// A single news source integration (AajTak, and in later phases ANI/NDTV/PIB/TheHindu/...).
/// Implementations only know how to download, parse, and normalize their own feeds -
/// they never persist anything themselves.
/// </summary>
public interface IRssProvider
{
    /// <summary>
    /// Provider key, must match a <see cref="RssProviderOptions.Name"/> entry in configuration.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Downloads, parses, and normalizes every configured/enabled feed for this provider.
    /// One <see cref="FeedFetchResult"/> is returned per feed so the caller can log/record
    /// per-feed success or failure without one bad feed aborting the whole run.
    /// </summary>
    Task<IReadOnlyList<FeedFetchResult>> FetchAllFeedsAsync(
        IReadOnlyList<RssFeedOptions> feeds,
        CancellationToken cancellationToken);
}
