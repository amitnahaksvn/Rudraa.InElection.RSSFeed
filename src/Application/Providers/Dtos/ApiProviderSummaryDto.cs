namespace Application.Providers.Dtos;

public sealed record ApiEndpointSummaryDto(string Name, string Endpoint, string Category, string Language, bool Enabled);

/// <summary>One <c>NewsApiProviderOptions</c> block flattened for the Provider Management page - <see cref="Enabled"/> already folds in the owning country's own flag, same reasoning as <see cref="RssProviderSummaryDto"/>.</summary>
public sealed record ApiProviderSummaryDto(
    string Country,
    string Name,
    bool Enabled,
    string Cron,
    string BaseUrl,
    string AuthType,
    string Description,
    IReadOnlyList<ApiEndpointSummaryDto> Endpoints);
