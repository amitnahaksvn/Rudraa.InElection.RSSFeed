using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Application.Models;
using Application.News.Dtos;
using Application.News.Queries.GetLatestNews;
using Application.News.Queries.GetNewsByCategory;
using Application.News.Queries.GetNewsByProvider;
using Application.News.Queries.GetNewsCountries;
using Application.News.Queries.GetNewsFeed;
using Application.News.Queries.GetNewsFeedCount;
using Application.News.Queries.SearchNews;
using Domain.Enums;
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
        group.MapGet("feed", GetFeed);
        group.MapGet("feed/count", GetFeedCount);
        group.MapGet("countries", GetCountries);
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

    [EndpointSummary("News feed, paged")]
    [EndpointDescription(
        "Newest-first articles for the News Feed page's infinite scroll - 'sourceType' (Rss/Api) " +
        "picks the tab, 'country' optionally narrows to one publisher country, 'sortBy' " +
        "(PublishedAt/CrawledAt, defaults to PublishedAt) picks which timestamp 'newest' means, and " +
        "'skip'/'count' page through the results as the reader scrolls.")]
    public static async Task<Ok<IReadOnlyList<NewsArticleDto>>> GetFeed(
        ISender sender, IOptions<ApiOptions> apiOptions, ArticleSourceType? sourceType, string? country, int skip, int count,
        NewsFeedSortBy sortBy = NewsFeedSortBy.PublishedAt, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetNewsFeedQuery(sourceType, country, Math.Max(0, skip), ResolvePageSize(count, apiOptions.Value), sortBy),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("News feed total count")]
    [EndpointDescription("Total articles matching the same 'sourceType'/'country' narrowing as the feed endpoint - backs the News Feed page's total-count header.")]
    public static async Task<Ok<long>> GetFeedCount(
        ISender sender, ArticleSourceType? sourceType, string? country, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNewsFeedCountQuery(sourceType, country), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Countries with active articles")]
    [EndpointDescription("Every distinct publisher country currently represented, optionally narrowed to one pipeline (Rss/Api) - backs the News Feed page's country filter.")]
    public static async Task<Ok<IReadOnlyList<string>>> GetCountries(
        ISender sender, ArticleSourceType? sourceType, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetNewsCountriesQuery(sourceType), cancellationToken);
        return TypedResults.Ok(result);
    }

    private static int ResolvePageSize(int requested, ApiOptions options) =>
        requested <= 0 ? options.DefaultPageSize : Math.Min(requested, options.MaxPageSize);
}
