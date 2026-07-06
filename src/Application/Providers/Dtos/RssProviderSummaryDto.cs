namespace Application.Providers.Dtos;

public sealed record RssFeedSummaryDto(string Name, string Url, string Category, string Language, bool Enabled);

/// <summary>One <c>RssProviderOptions</c> block flattened for the Provider Management page - <see cref="Enabled"/> already folds in the owning country's own flag, since that's what actually determines whether this provider's feeds are ever fetched.</summary>
public sealed record RssProviderSummaryDto(
    string Country,
    string Name,
    bool Enabled,
    string Cron,
    string Description,
    IReadOnlyList<RssFeedSummaryDto> Feeds);
