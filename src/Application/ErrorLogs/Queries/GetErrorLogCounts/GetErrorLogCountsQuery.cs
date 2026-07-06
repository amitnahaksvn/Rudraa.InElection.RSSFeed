using Mediator;
using Application.Abstractions;
using Application.ErrorLogs.Dtos;

namespace Application.ErrorLogs.Queries.GetErrorLogCounts;

/// <summary>Per-status and per-category counts for the error-monitor's left-hand sidebar, under the same non-status/non-category filters as <c>GetErrorLogsQuery</c> so the counts reflect an active search/provider/country/source filter.</summary>
public sealed record GetErrorLogCountsQuery(
    string? Provider = null,
    string? Country = null,
    string? Source = null,
    string? Search = null) : IRequest<ErrorLogCountsDto>;

public sealed class GetErrorLogCountsQueryHandler : IRequestHandler<GetErrorLogCountsQuery, ErrorLogCountsDto>
{
    private readonly IErrorLogRepository _errorLogs;

    public GetErrorLogCountsQueryHandler(IErrorLogRepository errorLogs)
    {
        _errorLogs = errorLogs;
    }

    public async ValueTask<ErrorLogCountsDto> Handle(GetErrorLogCountsQuery request, CancellationToken cancellationToken)
    {
        var baseFilter = new ErrorLogFilter(null, request.Provider, request.Country, request.Source, request.Search);

        var all = await _errorLogs.CountAsync(baseFilter, cancellationToken);
        var unresolved = await _errorLogs.CountAsync(baseFilter with { IsResolved = false }, cancellationToken);
        var resolved = await _errorLogs.CountAsync(baseFilter with { IsResolved = true }, cancellationToken);
        var rss = await _errorLogs.CountAsync(baseFilter with { Category = ErrorLogCategory.Rss }, cancellationToken);
        var api = await _errorLogs.CountAsync(baseFilter with { Category = ErrorLogCategory.Api }, cancellationToken);
        var social = await _errorLogs.CountAsync(baseFilter with { Category = ErrorLogCategory.Social }, cancellationToken);
        var http = await _errorLogs.CountAsync(baseFilter with { Category = ErrorLogCategory.Http }, cancellationToken);
        var critical = await _errorLogs.CountAsync(baseFilter with { Category = ErrorLogCategory.Critical }, cancellationToken);
        var warning = await _errorLogs.CountAsync(baseFilter with { Category = ErrorLogCategory.Warning }, cancellationToken);

        return new ErrorLogCountsDto(all, unresolved, resolved, rss, api, social, http, critical, warning);
    }
}
