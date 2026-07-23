using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Application.Providers.Commands.CreateCrawlFeed;
using Application.Providers.Commands.DeleteCountry;
using Application.Providers.Commands.DeleteCrawlFeed;
using Application.Providers.Commands.DeleteProviderSchedule;
using Application.Providers.Commands.TestApiEndpoint;
using Application.Providers.Commands.TestRssFeed;
using Application.Providers.Commands.UpdateCrawlFeed;
using Application.Providers.Commands.UpdateProviderSchedule;
using Application.Providers.Commands.UpsertCountry;
using Application.Providers.Dtos;
using Application.Providers.Queries.GetApiProviders;
using Application.Providers.Queries.GetCountries;
using Application.Providers.Queries.GetRssProviders;
using Domain.Enums;
using WebPlatform.Infrastructure;

namespace WebApp.Endpoints;

/// <summary>
/// Backs the Provider Management page: every database-backed country/provider/feed for both
/// pipelines, an on-demand connectivity/content test for any single feed/endpoint, and full CRUD
/// (add/edit/delete) at all three levels - country, provider (schedule), and feed/endpoint.
/// Read/write actions only; actual scheduled execution happens on RssService/ApiService, not here.
/// </summary>
public sealed class Providers : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/providers");

        group.MapGet("rss", GetRssProviders);
        group.MapGet("api", GetApiProviders);
        group.MapPost("rss/test", TestRssFeed);
        group.MapPost("api/test", TestApiEndpoint);

        group.MapGet("countries", GetCrawlCountries);
        group.MapPut("countries", UpsertCountry);
        group.MapDelete("countries/{pipeline}/{name}", DeleteCountry);

        group.MapPut("schedule", UpdateSchedule);
        group.MapDelete("schedule/{pipeline}/{provider}/{country}", DeleteSchedule);

        group.MapPost("feeds", CreateFeed);
        group.MapPut("feeds/{id}", UpdateFeed);
        group.MapDelete("feeds/{id}", DeleteFeed);
    }

    [EndpointSummary("List every configured RSS provider")]
    [EndpointDescription("Every RSS provider across every country, with its feeds, cron schedule, enabled status, and a computed description - fully database-backed.")]
    public static async Task<Ok<IReadOnlyList<RssProviderSummaryDto>>> GetRssProviders(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRssProvidersQuery(), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("List every configured JSON news-API provider")]
    [EndpointDescription("Every JSON news-API provider across every country, with its endpoints, cron schedule, enabled status, and a computed description - fully database-backed.")]
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
        var result = await sender.Send(new TestRssFeedCommand(request.FeedId), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Test one JSON news-API endpoint")]
    [EndpointDescription("Calls exactly one already-configured API endpoint right now, regardless of its own Enabled flag, and reports success/failure, HTTP status, article count, and elapsed time. Never persists anything - a pure diagnostic action.")]
    public static async Task<Ok<ProviderTestResultDto>> TestApiEndpoint(
        ISender sender, TestApiEndpointRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new TestApiEndpointCommand(request.EndpointId), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("List countries for a pipeline")]
    [EndpointDescription("Every database-backed country for 'Rss' or 'Api', with its own independent Enabled flag.")]
    public static async Task<Ok<IReadOnlyList<CountryDto>>> GetCrawlCountries(ISender sender, string pipeline, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCountriesQuery(ParsePipeline(pipeline)), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Add a country or change its enabled flag")]
    [EndpointDescription("Creates a brand-new country or overwrites an existing one's Enabled flag - disabling a country skips every provider under it entirely at the next crawl/registration cycle.")]
    public static async Task<Ok<CountryDto>> UpsertCountry(ISender sender, UpsertCountryRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpsertCountryCommand(ParsePipeline(request.Pipeline), request.Name, request.Enabled), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Delete a country")]
    [EndpointDescription("Removes a country. Providers/feeds that reference it are left alone - they just point at a country name that no longer exists, same as any other orphaned reference.")]
    public static async Task<Results<Ok, NotFound>> DeleteCountry(ISender sender, string pipeline, string name, CancellationToken cancellationToken)
    {
        var found = await sender.Send(new DeleteCountryCommand(ParsePipeline(pipeline), name), cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    [EndpointSummary("Enable/disable a provider or change its cron")]
    [EndpointDescription(
        "Creates a brand-new provider or overwrites an existing one's full catalog record - persists " +
        "to the database and immediately updates the live Hangfire recurring job: enabling " +
        "registers/reschedules it, disabling removes it. 'provider' must have a matching " +
        "IRssProvider/INewsApiProvider C# class already registered, and 'country' must already be " +
        "configured for the given 'pipeline'.")]
    public static async Task<Ok<ProviderScheduleDto>> UpdateSchedule(
        ISender sender, UpdateProviderScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProviderScheduleCommand(
                ParsePipeline(request.Pipeline),
                request.Provider,
                request.Country,
                request.Enabled,
                request.Cron,
                request.TimeZone ?? "UTC",
                request.SaveRawResponses ?? true,
                request.BaseUrl,
                ParseAuthType(request.AuthType),
                request.AuthParamName,
                request.TimeoutSeconds),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Delete a provider-country schedule")]
    [EndpointDescription("Removes one provider-country's catalog record entirely and its live Hangfire recurring job. Its feeds/endpoints are left orphaned in the feed catalog, same reasoning as deleting a country.")]
    public static async Task<Results<Ok, NotFound>> DeleteSchedule(ISender sender, string pipeline, string provider, string country, CancellationToken cancellationToken)
    {
        var found = await sender.Send(new DeleteProviderScheduleCommand(ParsePipeline(pipeline), provider, country), cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    [EndpointSummary("Add a feed or endpoint")]
    [EndpointDescription("Adds a new RSS feed or JSON-API endpoint under an existing provider-country schedule.")]
    public static async Task<Ok<CrawlFeedDto>> CreateFeed(ISender sender, CreateFeedRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCrawlFeedCommand(
                ParsePipeline(request.Pipeline),
                request.Provider,
                request.Country,
                request.Name,
                request.Url,
                request.Category,
                request.Language,
                request.Enabled,
                request.DefaultImageUrl,
                request.QueryParameters),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Edit a feed or endpoint")]
    [EndpointDescription("Full overwrite of an existing feed/endpoint's editable fields (Name/Url/Category/Language/Enabled/DefaultImageUrl/QueryParameters).")]
    public static async Task<Results<Ok, NotFound>> UpdateFeed(ISender sender, string id, UpdateFeedRequest request, CancellationToken cancellationToken)
    {
        var found = await sender.Send(
            new UpdateCrawlFeedCommand(id, request.Name, request.Url, request.Category, request.Language, request.Enabled, request.DefaultImageUrl, request.QueryParameters),
            cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    [EndpointSummary("Delete a feed or endpoint")]
    [EndpointDescription("Removes one feed/endpoint.")]
    public static async Task<Results<Ok, NotFound>> DeleteFeed(ISender sender, string id, CancellationToken cancellationToken)
    {
        var found = await sender.Send(new DeleteCrawlFeedCommand(id), cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    // System.Text.Json's default enum handling only accepts numbers in a request body (unlike
    // query-string/route enum binding, which already parses "Rss"/"Api" as text elsewhere in this
    // app) - Pipeline arrives as a plain string in every request here and is parsed by hand
    // instead of registering a global JsonStringEnumConverter for this one field. An unparseable
    // value becomes an out-of-range enum rather than silently defaulting to Rss, so each
    // command's own "Pipeline must be 'Rss' or 'Api'" validator rule still catches it as a normal
    // 400, not a raw model-binding failure.
    private static CrawlPipeline ParsePipeline(string pipeline) =>
        Enum.TryParse<CrawlPipeline>(pipeline, ignoreCase: true, out var parsed) ? parsed : (CrawlPipeline)(-1);

    private static Domain.Enums.ApiAuthType? ParseAuthType(string? authType) =>
        authType is not null && Enum.TryParse<Domain.Enums.ApiAuthType>(authType, ignoreCase: true, out var parsed) ? parsed : null;
}

public sealed record TestRssFeedRequest(string FeedId);

public sealed record TestApiEndpointRequest(string EndpointId);

public sealed record UpsertCountryRequest(string Pipeline, string Name, bool Enabled);

public sealed record UpdateProviderScheduleRequest(
    string Pipeline,
    string Provider,
    string Country,
    bool Enabled,
    string Cron,
    string? TimeZone,
    bool? SaveRawResponses,
    string? BaseUrl,
    string? AuthType,
    string? AuthParamName,
    int? TimeoutSeconds);

public sealed record CreateFeedRequest(
    string Pipeline,
    string Provider,
    string Country,
    string Name,
    string Url,
    string Category,
    string Language,
    bool Enabled,
    string? DefaultImageUrl,
    Dictionary<string, string>? QueryParameters);

public sealed record UpdateFeedRequest(
    string Name,
    string Url,
    string Category,
    string Language,
    bool Enabled,
    string? DefaultImageUrl,
    Dictionary<string, string>? QueryParameters);
