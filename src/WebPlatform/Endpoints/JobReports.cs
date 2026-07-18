using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.JobReports.Dtos;
using Application.JobReports.Queries.GetJobExecutionLogs;
using Domain.Enums;
using WebPlatform.Infrastructure;

namespace WebPlatform.Endpoints;

/// <summary>Execution history for generic (non-crawl) recurring jobs - the keep-alive self-ping, raw-response cleanup, error-notification dispatch. Crawl runs themselves have their own dedicated report - see <see cref="Crawl"/>.</summary>
public sealed class JobReports : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/job-reports");

        group.MapGet("", GetJobExecutionLogs);
    }

    [EndpointSummary("Generic job execution history")]
    [EndpointDescription(
        "Most recent executions of a generic (non-crawl) recurring job - keep-alive-ping, " +
        "cleanup-raw-responses, dispatch-error-notifications - newest first. Every filter beyond " +
        "'count' is optional: 'jobId' (the Hangfire recurring-job id), 'status' " +
        "(Running/Succeeded/Failed), and 'from'/'to' (an inclusive UTC date range on StartedAt).")]
    public static async Task<Ok<IReadOnlyList<JobExecutionLogDto>>> GetJobExecutionLogs(
        ISender sender,
        int count,
        string? jobId,
        JobExecutionStatus? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? skip,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetJobExecutionLogsQuery(count <= 0 ? 20 : count, jobId, status, from, to, Math.Max(0, skip ?? 0)),
            cancellationToken);
        return TypedResults.Ok(result);
    }
}
