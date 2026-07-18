using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.Crawl.Commands.CreateOrUpdateRecurringJob;
using Application.Crawl.Commands.TriggerApiCrawl;
using Application.Crawl.Commands.TriggerCrawl;
using Application.Crawl.Commands.TriggerProviderJob;
using Application.Crawl.Dtos;
using Domain.Enums;
using WebPlatform.Infrastructure;

namespace WebApp.Endpoints;

/// <summary>
/// Manual crawl triggering and recurring-job creation, for both pipelines - unlike
/// <see cref="WebPlatform.Endpoints.Crawl"/>'s read-only status/history/report endpoints (which
/// already take a caller-supplied pipeline), these actually enqueue/run work, so WebApp - the only
/// process that owns this file, since it's the only one talking to callers - decides which
/// pipeline via an explicit request field/route rather than a value baked in per host, the way it
/// worked back when RSS and API each had their own copy of this file. Actual execution still only
/// ever happens on RssService or ApiService, wherever that pipeline's queue is being watched -
/// this endpoint only enqueues/registers against the shared Hangfire storage.
/// </summary>
public sealed class CrawlTrigger : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/crawl");

        group.MapPost("trigger/rss", TriggerRss);
        group.MapPost("trigger/api", TriggerApi);
        group.MapPost("trigger/{provider}", TriggerProvider);
        group.MapPost("jobs", CreateOrUpdateJob);
    }

    [EndpointSummary("Trigger an RSS crawl")]
    [EndpointDescription(
        "Runs an RSS crawl immediately and waits for it to finish. Subject to the same distributed " +
        "lock as the scheduled jobs, so a run already in progress is skipped (409) rather than run concurrently.")]
    public static async Task<Results<Ok<CrawlHistoryDto>, Conflict<CrawlHistoryDto>>> TriggerRss(
        ISender sender, CancellationToken cancellationToken)
    {
        var history = await sender.Send(new TriggerCrawlCommand(), cancellationToken);
        return history.WasSkipped ? TypedResults.Conflict(history) : TypedResults.Ok(history);
    }

    [EndpointSummary("Trigger a JSON news-API crawl")]
    [EndpointDescription(
        "Runs a JSON news-API crawl immediately and waits for it to finish. Subject to the same " +
        "distributed lock as the scheduled jobs, so a run already in progress is skipped (409) " +
        "rather than run concurrently.")]
    public static async Task<Results<Ok<CrawlHistoryDto>, Conflict<CrawlHistoryDto>>> TriggerApi(
        ISender sender, CancellationToken cancellationToken)
    {
        var history = await sender.Send(new TriggerApiCrawlCommand(), cancellationToken);
        return history.WasSkipped ? TypedResults.Conflict(history) : TypedResults.Ok(history);
    }

    [EndpointSummary("Trigger a single provider's recurring job")]
    [EndpointDescription(
        "Enqueues one provider's own Hangfire recurring job to run now, ahead of its cron schedule, " +
        "without changing that schedule. 'pipeline' picks RSS vs JSON-API and defaults to Rss. " +
        "Unlike the bulk trigger endpoints above this does not wait for the crawl to finish - it " +
        "only confirms the job was enqueued; actual execution happens wherever that job's Hangfire " +
        "server (RssService or ApiService) is running, guarded by the same distributed lock.")]
    public static async Task<Ok<ProviderJobTriggeredDto>> TriggerProvider(
        ISender sender, string provider, CrawlPipeline? pipeline, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TriggerProviderJobCommand(pipeline ?? CrawlPipeline.Rss, provider), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Create or update a provider's recurring job")]
    [EndpointDescription(
        "Registers (or updates, if it already exists) a provider's Hangfire recurring crawl job - " +
        "body: { \"pipeline\": \"Rss\", \"jobName\": \"AajTak\", \"cron\": \"*/10 * * * *\", " +
        "\"timeZone\": \"UTC\" } (timeZone defaults to UTC if omitted). jobName must already be an " +
        "enabled provider for the given pipeline - this schedules crawling that provider, not " +
        "arbitrary code. This is a live override: it takes effect immediately but does not persist " +
        "to config, so RssService/ApiService's next restart re-syncs every provider's job back to " +
        "whatever the config (and ProviderSchedule) says.")]
    public static async Task<Ok<CrawlRecurringJobDto>> CreateOrUpdateJob(
        ISender sender, CreateOrUpdateRecurringJobRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateOrUpdateRecurringJobCommand(request.Pipeline, request.JobName, request.Cron, request.TimeZone ?? "UTC"),
            cancellationToken);
        return TypedResults.Ok(result);
    }
}

public sealed record CreateOrUpdateRecurringJobRequest(CrawlPipeline Pipeline, string JobName, string Cron, string? TimeZone);
