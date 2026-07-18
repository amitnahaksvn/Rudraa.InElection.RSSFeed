using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.Crawl.Dtos;
using Application.Crawl.Queries.GetCrawlHistory;
using Application.Crawl.Queries.GetCrawlHistoryById;
using Application.Crawl.Queries.GetCrawlJobStatus;
using Application.Crawl.Queries.GetCrawlReport;
using Domain.Enums;
using WebPlatform.Infrastructure;

namespace WebPlatform.Endpoints;

/// <summary>
/// Recurring-job status, crawl run history, and the crawl-report page's data - all genuinely
/// pipeline-agnostic (each takes an explicit/defaultable <see cref="CrawlPipeline"/> parameter and
/// reads the shared <c>CrawlHistory</c>/Hangfire-storage Mongo collections), so shared between
/// WebRssFeed and WebApiFeed rather than duplicated. Manual crawl *triggering* (which pipeline's
/// crawler service or provider list a request acts against is fixed per host, not caller-supplied)
/// lives in each host's own <c>Endpoints/CrawlTrigger.cs</c> instead - see its own doc comment.
/// </summary>
public sealed class Crawl : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/crawl");

        group.MapGet("jobs/{provider}", GetJobStatus);
        group.MapGet("history", GetHistory);
        group.MapGet("history/{id}", GetHistoryById);
        group.MapGet("report", GetReport);
    }

    [EndpointSummary("Get a provider's recurring job status")]
    [EndpointDescription(
        "Current schedule plus the outcome of the most recent run for one provider's recurring job: " +
        "next/last execution time, the Hangfire job id and state (Succeeded/Failed/Processing/...) of " +
        "that last run, and exception details if it failed. 'pipeline' picks which job-id scheme to " +
        "look up (RSS providers and API providers register under different recurring-job ids) and " +
        "defaults to Rss. 404 if no recurring job is registered for that provider name.")]
    public static async Task<Results<Ok<CrawlJobStatusDto>, NotFound>> GetJobStatus(
        ISender sender, string provider, CrawlPipeline? pipeline, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCrawlJobStatusQuery(provider, pipeline ?? CrawlPipeline.Rss), cancellationToken);
        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    [EndpointSummary("Crawl run history")]
    [EndpointDescription(
        "Most recent crawl run records, newest first. Every filter beyond 'count' is optional: " +
        "'pipeline' (Rss/Api/Social), 'provider' (an exact provider name), 'from'/'to' (an " +
        "inclusive UTC date range), and 'skip' (page offset) narrow it down to one " +
        "pipeline/provider/window/page - e.g. the crawl-report page's recent-runs table for " +
        "whichever tab, date range, and page is selected.")]
    public static async Task<Ok<IReadOnlyList<CrawlHistoryDto>>> GetHistory(
        ISender sender, int count, CrawlPipeline? pipeline, string? provider, DateTimeOffset? from, DateTimeOffset? to, int? skip,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCrawlHistoryQuery(count <= 0 ? 20 : count, pipeline, provider, from, to, Math.Max(0, skip ?? 0)), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get a single crawl run by id")]
    [EndpointDescription("Full detail for one crawl run record. 404 if no run with that id exists.")]
    public static async Task<Results<Ok<CrawlHistoryDto>, NotFound>> GetHistoryById(
        ISender sender, string id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCrawlHistoryByIdQuery(id), cancellationToken);
        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    [EndpointSummary("Crawl report for one pipeline")]
    [EndpointDescription(
        "Headline stats, a daily time series, and a per-provider breakdown (schedule, next/last " +
        "run, success rate, articles saved/skipped) for either the RSS or API pipeline over a date " +
        "range - backs the crawl-report page's two tabs. 'from'/'to' default to the trailing 7 days " +
        "when omitted and the range cannot exceed 365 days.")]
    public static async Task<Ok<CrawlReportDto>> GetReport(
        ISender sender, CrawlPipeline pipeline, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCrawlReportQuery(pipeline, from, to), cancellationToken);
        return TypedResults.Ok(result);
    }
}
