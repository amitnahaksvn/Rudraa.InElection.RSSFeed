using Mediator;
using Application.Abstractions;
using Application.ErrorLogs.Dtos;

namespace Application.ErrorLogs.Queries.GetErrorLogProviderBreakdown;

/// <summary>
/// Feed/provider-wise error breakdown for the error-monitor sidebar: one entry per pipeline
/// (Rss/Api/Social/Http - see <see cref="ErrorLogCategory"/>), each carrying the providers/feeds
/// that produced errors within it (e.g. AajTak, ABPNews under Rss) with each one's own count -
/// under the same optional provider/country/source/search filters as <c>GetErrorLogCountsQuery</c>.
/// </summary>
public sealed record GetErrorLogProviderBreakdownQuery(
    string? Provider = null,
    string? Country = null,
    string? Source = null,
    string? Search = null) : IRequest<IReadOnlyList<ErrorLogCategoryBreakdownDto>>;

public sealed class GetErrorLogProviderBreakdownQueryHandler : IRequestHandler<GetErrorLogProviderBreakdownQuery, IReadOnlyList<ErrorLogCategoryBreakdownDto>>
{
    // Rss deliberately covers both literal Source strings the same way BuildCategoryFilter does -
    // RSS-proper and dynamic (Mongo-driven) feeds are both "a feed fetch failed" from this view's
    // point of view. "Crawl Run" has no per-provider breakdown of its own (it's a whole-run
    // failure, not tied to one feed), so it's intentionally left unmapped rather than forced into
    // one of the four pipeline buckets.
    private static readonly IReadOnlyDictionary<string, ErrorLogCategory> SourceToCategory = new Dictionary<string, ErrorLogCategory>(StringComparer.Ordinal)
    {
        ["RSS Feed Fetch"] = ErrorLogCategory.Rss,
        ["Dynamic Feed Fetch"] = ErrorLogCategory.Rss,
        ["News API Fetch"] = ErrorLogCategory.Api,
        ["Social Media Fetch"] = ErrorLogCategory.Social,
        ["HTTP Request"] = ErrorLogCategory.Http,
    };

    private static readonly ErrorLogCategory[] PipelineCategories = [ErrorLogCategory.Rss, ErrorLogCategory.Api, ErrorLogCategory.Social, ErrorLogCategory.Http];

    private readonly IErrorLogRepository _errorLogs;

    public GetErrorLogProviderBreakdownQueryHandler(IErrorLogRepository errorLogs)
    {
        _errorLogs = errorLogs;
    }

    public async ValueTask<IReadOnlyList<ErrorLogCategoryBreakdownDto>> Handle(GetErrorLogProviderBreakdownQuery request, CancellationToken cancellationToken)
    {
        var filter = new ErrorLogFilter(null, request.Provider, request.Country, request.Source, request.Search);
        var rawCounts = await _errorLogs.GetProviderCountsAsync(filter, cancellationToken);

        return PipelineCategories
            .Select(category =>
            {
                var providers = rawCounts
                    .Where(r => SourceToCategory.TryGetValue(r.Source, out var mapped) && mapped == category)
                    .GroupBy(r => r.Provider, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ErrorLogProviderGroupDto(g.Key, g.Sum(r => r.Count), g.Sum(r => r.UnresolvedCount)))
                    .OrderByDescending(p => p.Count)
                    .ThenBy(p => p.Provider, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ErrorLogCategoryBreakdownDto(
                    category,
                    providers.Sum(p => p.Count),
                    providers.Sum(p => p.UnresolvedCount),
                    providers);
            })
            .ToList();
    }
}
