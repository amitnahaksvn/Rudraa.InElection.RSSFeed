using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Application.ErrorLogs.Dtos;
using Application.FilteredArticles.Commands.DeleteFilteredArticle;
using Application.FilteredArticles.Dtos;
using Application.FilteredArticles.Queries.GetFilteredArticles;
using WebPlatform.Infrastructure;
using WebPlatform.Options;

namespace WebPlatform.Endpoints;

/// <summary>Backs the Filtered Articles admin page: a paged, newest-first log of articles excluded by the political-category allowlist (<c>NewsFilterOptions</c>), plus per-row delete.</summary>
public sealed class FilteredArticles : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/filtered-articles");

        group.MapGet("", GetList);
        group.MapDelete("{id}", Delete);
    }

    [EndpointSummary("List filtered articles")]
    [EndpointDescription("Paged, newest-first log of articles that were fetched but excluded because their Category wasn't in the political allowlist.")]
    public static async Task<Ok<PagedResult<FilteredArticleDto>>> GetList(
        ISender sender,
        IOptions<ApiOptions> apiOptions,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = ResolvePageSize(pageSize, apiOptions.Value);

        var result = await sender.Send(new GetFilteredArticlesQuery(resolvedPage, resolvedPageSize), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Delete a filtered article row")]
    [EndpointDescription("Removes one row from the filtered-articles log - a hard delete, since these are diagnostic records, not real articles.")]
    public static async Task<Results<Ok, NotFound>> Delete(ISender sender, string id, CancellationToken cancellationToken)
    {
        var found = await sender.Send(new DeleteFilteredArticleCommand(id), cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    private static int ResolvePageSize(int requested, ApiOptions options) =>
        requested <= 0 ? options.DefaultPageSize : Math.Min(requested, options.MaxPageSize);
}
