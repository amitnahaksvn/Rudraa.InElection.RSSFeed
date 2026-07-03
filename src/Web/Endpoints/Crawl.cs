using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.Crawl.Commands.CreateOrUpdateRecurringJob;
using Application.Crawl.Commands.TriggerCrawl;
using Application.Crawl.Commands.TriggerProviderJob;
using Application.Crawl.Dtos;
using Application.Crawl.Queries.GetCrawlHistory;
using Application.Crawl.Queries.GetCrawlHistoryById;
using Application.Crawl.Queries.GetCrawlJobStatus;
using Web.Infrastructure;

namespace Web.Endpoints;

/// <summary>Manual crawl triggering, recurring-job management, and crawl run history.</summary>
public sealed class Crawl : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/crawl");

        group.MapPost("trigger", Trigger);
        group.MapPost("trigger/{provider}", TriggerProvider);
        group.MapPost("jobs", CreateOrUpdateJob);
        group.MapGet("jobs/{provider}", GetJobStatus);
        group.MapGet("history", GetHistory);
        group.MapGet("history/{id}", GetHistoryById);
    }

    [EndpointSummary("Trigger a crawl")]
    [EndpointDescription(
        "Runs a crawl immediately and waits for it to finish. Subject to the same distributed " +
        "lock as the scheduled worker, so a run already in progress is skipped (409) rather than run concurrently.")]
    public static async Task<Results<Ok<CrawlHistoryDto>, Conflict<CrawlHistoryDto>>> Trigger(
        ISender sender, CancellationToken cancellationToken)
    {
        var history = await sender.Send(new TriggerCrawlCommand(), cancellationToken);
        return history.WasSkipped ? TypedResults.Conflict(history) : TypedResults.Ok(history);
    }

    [EndpointSummary("Trigger a single provider's recurring job")]
    [EndpointDescription(
        "Enqueues one provider's own Hangfire recurring job to run now, ahead of its cron schedule, " +
        "without changing that schedule. Unlike POST /trigger this does not wait for the crawl to " +
        "finish - it only confirms the job was enqueued; actual execution happens wherever that " +
        "job's Hangfire server is running, guarded by the same distributed lock.")]
    public static async Task<Ok<ProviderJobTriggeredDto>> TriggerProvider(
        ISender sender, string provider, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TriggerProviderJobCommand(provider), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Create or update a provider's recurring job")]
    [EndpointDescription(
        "Registers (or updates, if it already exists) a provider's Hangfire recurring crawl job - " +
        "body: { \"jobName\": \"AajTak\", \"cron\": \"*/10 * * * *\", \"timeZone\": \"UTC\" } (timeZone " +
        "defaults to UTC if omitted). jobName must already be an enabled provider under " +
        "NewsCrawler:Providers - this schedules crawling that provider, not arbitrary code. This is a " +
        "live override: it takes effect immediately but does not persist to NewsCrawler.appsettings.json, " +
        "so this process's next restart re-syncs every provider's job back to whatever that file says.")]
    public static async Task<Ok<CrawlRecurringJobDto>> CreateOrUpdateJob(
        ISender sender, CreateOrUpdateRecurringJobCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get a provider's recurring job status")]
    [EndpointDescription(
        "Current schedule plus the outcome of the most recent run for one provider's recurring job: " +
        "next/last execution time, the Hangfire job id and state (Succeeded/Failed/Processing/...) of " +
        "that last run, and exception details if it failed. 404 if no recurring job is registered for " +
        "that provider name.")]
    public static async Task<Results<Ok<CrawlJobStatusDto>, NotFound>> GetJobStatus(
        ISender sender, string provider, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCrawlJobStatusQuery(provider), cancellationToken);
        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    [EndpointSummary("Crawl run history")]
    [EndpointDescription("Most recent crawl run records, newest first.")]
    public static async Task<Ok<IReadOnlyList<CrawlHistoryDto>>> GetHistory(
        ISender sender, int count, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCrawlHistoryQuery(count <= 0 ? 20 : count), cancellationToken);
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
}
