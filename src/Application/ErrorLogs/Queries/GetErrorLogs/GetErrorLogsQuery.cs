using Mediator;
using Application.Abstractions;
using Application.ErrorLogs.Dtos;

namespace Application.ErrorLogs.Queries.GetErrorLogs;

/// <summary>Page of error-monitor rows, unresolved-first then newest-first (see <see cref="IErrorLogRepository.GetPagedAsync"/>). <paramref name="Page"/> is 1-based.</summary>
public sealed record GetErrorLogsQuery(
    int Page,
    int PageSize,
    bool? IsResolved = null,
    string? Provider = null,
    string? Country = null,
    string? Source = null,
    string? Search = null,
    ErrorLogCategory? Category = null) : IRequest<PagedResult<ErrorLogSummaryDto>>;

public sealed class GetErrorLogsQueryHandler : IRequestHandler<GetErrorLogsQuery, PagedResult<ErrorLogSummaryDto>>
{
    private readonly IErrorLogRepository _errorLogs;

    public GetErrorLogsQueryHandler(IErrorLogRepository errorLogs)
    {
        _errorLogs = errorLogs;
    }

    public async ValueTask<PagedResult<ErrorLogSummaryDto>> Handle(GetErrorLogsQuery request, CancellationToken cancellationToken)
    {
        var filter = new ErrorLogFilter(request.IsResolved, request.Provider, request.Country, request.Source, request.Search, request.Category);
        var skip = (request.Page - 1) * request.PageSize;

        var logs = await _errorLogs.GetPagedAsync(filter, skip, request.PageSize, cancellationToken);
        var totalCount = await _errorLogs.CountAsync(filter, cancellationToken);

        return new PagedResult<ErrorLogSummaryDto>(
            logs.Select(ErrorLogSummaryDto.FromDomain).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
