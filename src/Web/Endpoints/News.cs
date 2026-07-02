using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Application.News.Dtos;
using Application.News.Queries.GetLatestNews;
using Application.News.Queries.GetNewsByCategory;
using Application.News.Queries.GetNewsByProvider;
using Application.News.Queries.SearchNews;
using Web.Infrastructure;
using Web.Options;

namespace Web.Endpoints;

/// <summary>Read-only access to crawled news articles.</summary>
public sealed class News : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/news");

        group.MapGet("latest", GetLatest);
        group.MapGet("provider/{provider}", GetByProvider);
        group.MapGet("category/{category}", GetByCategory);
        group.MapGet("search", Search);
    }

    [EndpointSummary("Latest articles")]
    [EndpointDescription("Latest articles across every provider, newest first.")]
    public static async Task<Ok<IReadOnlyList<NewsArticleDto>>> GetLatest(
        ISender sender, IOptions<ApiOptions> apiOptions, int count, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLatestNewsQuery(ResolvePageSize(count, apiOptions.Value)), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Articles by provider")]
    [EndpointDescription("Latest articles from a single provider (e.g. \"AajTak\").")]
    public static async Task<Ok<IReadOnlyList<NewsArticleDto>>> GetByProvider(
        ISender sender, IOptions<ApiOptions> apiOptions, string provider, int count, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetNewsByProviderQuery(provider, ResolvePageSize(count, apiOptions.Value)), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Articles by category")]
    [EndpointDescription("Latest articles in a single category (e.g. \"Politics\").")]
    public static async Task<Ok<IReadOnlyList<NewsArticleDto>>> GetByCategory(
        ISender sender, IOptions<ApiOptions> apiOptions, string category, int count, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetNewsByCategoryQuery(category, ResolvePageSize(count, apiOptions.Value)), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Search articles")]
    [EndpointDescription("Full-text (title/summary) search across every article.")]
    public static async Task<Ok<IReadOnlyList<NewsArticleDto>>> Search(
        ISender sender, IOptions<ApiOptions> apiOptions, string q, int count, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SearchNewsQuery(q, ResolvePageSize(count, apiOptions.Value)), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static int ResolvePageSize(int requested, ApiOptions options) =>
        requested <= 0 ? options.DefaultPageSize : Math.Min(requested, options.MaxPageSize);
}
