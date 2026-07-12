using Mediator;
using Application.Abstractions;
using Application.JobReports.Dtos;
using Application.Models;
using Domain.Enums;

namespace Application.JobReports.Queries.GetJobExecutionLogs;

/// <summary>Backs the job-report page - every filter beyond <paramref name="Count"/> is optional, same convention as GetCrawlHistoryQuery.</summary>
public sealed record GetJobExecutionLogsQuery(
    int Count,
    string? JobId = null,
    JobExecutionStatus? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Skip = 0) : IRequest<IReadOnlyList<JobExecutionLogDto>>;

public sealed class GetJobExecutionLogsQueryHandler : IRequestHandler<GetJobExecutionLogsQuery, IReadOnlyList<JobExecutionLogDto>>
{
    private readonly IJobExecutionLogRepository _repository;

    public GetJobExecutionLogsQueryHandler(IJobExecutionLogRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<IReadOnlyList<JobExecutionLogDto>> Handle(GetJobExecutionLogsQuery request, CancellationToken cancellationToken)
    {
        var filter = new JobExecutionLogFilter(request.JobId, request.Status, request.From, request.To, request.Skip, request.Count);
        var logs = await _repository.GetFilteredAsync(filter, cancellationToken);
        return logs.Select(JobExecutionLogDto.FromDomain).ToList();
    }
}
