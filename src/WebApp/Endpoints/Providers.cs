using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.Providers.Commands.TestApiEndpoint;
using Application.Providers.Commands.TestRssFeed;
using Application.Providers.Commands.UpdateProviderSchedule;
using Application.Providers.Dtos;
using Application.Providers.Queries.GetApiProviders;
using Application.Providers.Queries.GetRssProviders;
using Domain.Enums;
using WebPlatform.Infrastructure;

namespace WebApp.Endpoints;

/// <summary>Backs the Provider Management page: every configured RSS feed/JSON-API endpoint (name, enabled status, cron, description), an on-demand connectivity/content test for any single one of them, and live schedule editing (enable/disable, change cron). Read-only reflection/test actions only - actual scheduled execution happens on RssService/ApiService, not here.</summary>
public sealed class Providers : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/providers");

        group.MapGet("rss", GetRssProviders);
        group.MapGet("api", GetApiProviders);
        group.MapPost("rss/test", TestRssFeed);
        group.MapPost("api/test", TestApiEndpoint);
        group.MapPut("schedule", UpdateSchedule);
    }

    [EndpointSummary("List every configured RSS provider")]
    [EndpointDescription("Every RSS provider across every country, with its feeds, cron schedule, enabled status, and a computed description - pure configuration reflection, no I/O.")]
    public static async Task<Ok<IReadOnlyList<RssProviderSummaryDto>>> GetRssProviders(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRssProvidersQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("List every configured JSON news-API provider")]
    [EndpointDescription("Every JSON news-API provider across every country, with its endpoints, cron schedule, enabled status, and a computed description - pure configuration reflection, no I/O.")]
    public static async Task<Ok<IReadOnlyList<ApiProviderSummaryDto>>> GetApiProviders(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetApiProvidersQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Test one RSS feed")]
    [EndpointDescription("Fetches and parses exactly one already-configured feed right now, regardless of its own Enabled flag, and reports success/failure, HTTP status, article count, and elapsed time. Never persists anything - a pure diagnostic action.")]
    public static async Task<Ok<ProviderTestResultDto>> TestRssFeed(
        ISender sender, TestRssFeedRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TestRssFeedCommand(request.Country, request.Provider, request.FeedUrl), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Test one JSON news-API endpoint")]
    [EndpointDescription("Calls exactly one already-configured API endpoint right now, regardless of its own Enabled flag, and reports success/failure, HTTP status, article count, and elapsed time. Never persists anything - a pure diagnostic action.")]
    public static async Task<Ok<ProviderTestResultDto>> TestApiEndpoint(
        ISender sender, TestApiEndpointRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TestApiEndpointCommand(request.Country, request.Provider, request.EndpointName), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Enable/disable a provider or change its cron")]
    [EndpointDescription(
        "Creates or overwrites one RSS/API provider's schedule - persists to the database (survives " +
        "restarts, no config-file edit needed) and immediately updates the live Hangfire recurring " +
        "job: enabling registers/reschedules it, disabling removes it. 'provider' must already be " +
        "configured under an enabled country for the given 'pipeline'. The live Hangfire job runs " +
        "on whichever of RssService/ApiService actually listens to that pipeline's queue - this " +
        "endpoint only manages the job's registration, it never executes one itself.")]
    public static async Task<Ok<ProviderScheduleDto>> UpdateSchedule(
        ISender sender, UpdateProviderScheduleRequest request, CancellationToken cancellationToken)
    {
        // System.Text.Json's default enum handling only accepts numbers in a request body (unlike
        // query-string/route enum binding, which already parses "Rss"/"Api" as text elsewhere in
        // this app) - Pipeline arrives as a plain string here and is parsed by hand instead of
        // registering a global JsonStringEnumConverter for one field. An unparseable value becomes
        // an out-of-range enum rather than silently defaulting to Rss, so
        // UpdateProviderScheduleCommandValidator's own "Pipeline must be 'Rss' or 'Api'" check
        // still catches it as a normal 400, not a raw model-binding failure.
        var pipeline = Enum.TryParse<CrawlPipeline>(request.Pipeline, ignoreCase: true, out var parsed) ? parsed : (CrawlPipeline)(-1);

        var result = await sender.Send(
            new UpdateProviderScheduleCommand(pipeline, request.Provider, request.Enabled, request.Cron, request.TimeZone ?? "UTC"),
            cancellationToken);
        return TypedResults.Ok(result);
    }
}

public sealed record TestRssFeedRequest(string Country, string Provider, string FeedUrl);

public sealed record TestApiEndpointRequest(string Country, string Provider, string EndpointName);

public sealed record UpdateProviderScheduleRequest(string Pipeline, string Provider, bool Enabled, string Cron, string? TimeZone);
