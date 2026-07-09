using Application.Abstractions;

namespace Application.ErrorLogs.Dtos;

/// <summary>One provider/feed's error count within a pipeline category breakdown - e.g. "AajTak" or "ABPNews" under <see cref="ErrorLogCategory.Rss"/>. See <see cref="ErrorLogCategoryBreakdownDto"/>.</summary>
public sealed record ErrorLogProviderGroupDto(string Provider, long Count, long UnresolvedCount);

/// <summary>Errors for one pipeline category (Rss/Api/Social/Http), grouped by which provider/feed they came from - the "which feed is failing" view, e.g. Rss -> [AajTak, ABPNews, ...], Api -> [NewsApiOrg, GNews, ...].</summary>
public sealed record ErrorLogCategoryBreakdownDto(
    ErrorLogCategory Category,
    long TotalCount,
    long UnresolvedCount,
    IReadOnlyList<ErrorLogProviderGroupDto> Providers);
