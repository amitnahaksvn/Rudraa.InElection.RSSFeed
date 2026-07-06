using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.Providers.Commands.TestApiEndpoint;
using Application.Providers.Commands.TestRssFeed;
using Application.Providers.Dtos;
using Application.Providers.Queries.GetApiProviders;
using Application.Providers.Queries.GetRssProviders;
using Web.Infrastructure;

namespace Web.Endpoints;

/// <summary>Backs the Provider Management page: every configured RSS feed/JSON-API endpoint (name, enabled status, cron, description) plus an on-demand connectivity/content test for any single one of them.</summary>
public sealed class Providers : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/providers");

        group.MapGet("rss", GetRssProviders);
        group.MapGet("api", GetApiProviders);
        group.MapPost("rss/test", TestRssFeed);
        group.MapPost("api/test", TestApiEndpoint);
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
}

public sealed record TestRssFeedRequest(string Country, string Provider, string FeedUrl);

public sealed record TestApiEndpointRequest(string Country, string Provider, string EndpointName);
