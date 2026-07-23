using Domain.Entities;

namespace Application.Providers.Dtos;

public sealed record CrawlFeedDto(
    string Id,
    string Provider,
    string Country,
    string Name,
    string Url,
    string Category,
    string Language,
    bool Enabled,
    string? DefaultImageUrl,
    Dictionary<string, string>? QueryParameters)
{
    public static CrawlFeedDto FromDomain(CrawlFeed feed) => new(
        feed.Id, feed.Provider, feed.Country, feed.Name, feed.Url, feed.Category, feed.Language, feed.Enabled, feed.DefaultImageUrl, feed.QueryParameters);
}
