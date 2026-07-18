using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.ErrorLogs.Commands.AddErrorLogComment;
using Application.ErrorLogs.Commands.SetErrorLogResolved;
using Application.ErrorLogs.Dtos;
using Application.ErrorLogs.Queries.GetErrorLogById;
using Application.ErrorLogs.Queries.GetErrorLogCounts;
using Application.ErrorLogs.Queries.GetErrorLogProviderBreakdown;
using Application.ErrorLogs.Queries.GetErrorLogs;
using WebPlatform.Infrastructure;
using WebPlatform.Options;

namespace WebPlatform.Endpoints;

/// <summary>Backs the error-monitor page: paged/filterable list of persisted <c>ErrorLog</c> rows, per-status counts, single-error detail, marking a row resolved/unresolved (with a required comment), and standalone comments.</summary>
public sealed class ErrorLogs : IEndpointGroup
{
    public static void Map(RouteGroupBuilder groupBuilder)
    {
        var group = groupBuilder.MapGroup("api/errors");

        group.MapGet("", GetList);
        group.MapGet("counts", GetCounts);
        group.MapGet("breakdown", GetProviderBreakdown);
        group.MapGet("{id}", GetById);
        group.MapPatch("{id}/resolved", SetResolved);
        group.MapPost("{id}/comments", AddComment);
    }

    [EndpointSummary("List errors")]
    [EndpointDescription(
        "Paged, filterable error-log rows for the error-monitor UI - unresolved rows first, newest " +
        "first within each group. All filter parameters are optional.")]
    public static async Task<Ok<PagedResult<ErrorLogSummaryDto>>> GetList(
        ISender sender,
        IOptions<ApiOptions> apiOptions,
        int page,
        int pageSize,
        bool? isResolved,
        string? provider,
        string? country,
        string? source,
        string? search,
        ErrorLogCategory? category,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = ResolvePageSize(pageSize, apiOptions.Value);

        var result = await sender.Send(
            new GetErrorLogsQuery(resolvedPage, resolvedPageSize, isResolved, provider, country, source, search, category),
            cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get per-status and per-category error counts")]
    [EndpointDescription("All/Unresolved/Resolved plus Rss/Api/Social/Http/Critical/Warning counts for the left-hand sidebar, under the same optional provider/country/source/search filters as the list endpoint.")]
    public static async Task<Ok<ErrorLogCountsDto>> GetCounts(
        ISender sender,
        string? provider,
        string? country,
        string? source,
        string? search,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetErrorLogCountsQuery(provider, country, source, search), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get feed/provider-wise error breakdown")]
    [EndpointDescription("Errors grouped by pipeline (Rss/Api/Social/Http) and, within each, by the specific provider/feed they came from (e.g. AajTak, ABPNews under Rss) with each provider's own error count - under the same optional provider/country/source/search filters as the counts endpoint.")]
    public static async Task<Ok<IReadOnlyList<ErrorLogCategoryBreakdownDto>>> GetProviderBreakdown(
        ISender sender,
        string? provider,
        string? country,
        string? source,
        string? search,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetErrorLogProviderBreakdownQuery(provider, country, source, search), cancellationToken);
        return TypedResults.Ok(result);
    }

    [EndpointSummary("Get one error's full detail")]
    [EndpointDescription("Every field for a single error, including stack trace, request/response bodies, and its comment/status history - only fetched when a row is selected.")]
    public static async Task<Results<Ok<ErrorLogDetailDto>, NotFound>> GetById(
        ISender sender, string id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetErrorLogByIdQuery(id), cancellationToken);
        return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
    }

    [EndpointSummary("Mark an error resolved or unresolved")]
    [EndpointDescription("Body: { \"resolved\": true, \"comment\": \"...\", \"description\": \"...\" } - comment is required and recorded in the row's history, description is optional. Used by the inline toggle in both the compact list row and the detail pane. The X-ErrorLog-Client-Id header (if sent) is echoed back over SignalR so the originating browser tab can skip re-applying its own live-update broadcast.")]
    public static async Task<Results<Ok, NotFound>> SetResolved(
        ISender sender,
        string id,
        SetResolvedRequest request,
        [FromHeader(Name = "X-ErrorLog-Client-Id")] string? clientId,
        CancellationToken cancellationToken)
    {
        var found = await sender.Send(
            new SetErrorLogResolvedCommand(id, request.Resolved, request.Comment, request.Description, clientId),
            cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    [EndpointSummary("Add a comment to an error")]
    [EndpointDescription("Body: { \"comment\": \"...\", \"description\": \"...\" } - comment is required, description optional. Appends to the row's history without changing its resolved status - lets a user leave multiple notes on the same error over time.")]
    public static async Task<Results<Ok, NotFound>> AddComment(
        ISender sender,
        string id,
        AddCommentRequest request,
        [FromHeader(Name = "X-ErrorLog-Client-Id")] string? clientId,
        CancellationToken cancellationToken)
    {
        var found = await sender.Send(new AddErrorLogCommentCommand(id, request.Comment, request.Description, clientId), cancellationToken);
        return found ? TypedResults.Ok() : TypedResults.NotFound();
    }

    private static int ResolvePageSize(int requested, ApiOptions options) =>
        requested <= 0 ? options.DefaultPageSize : Math.Min(requested, options.MaxPageSize);
}

public sealed record SetResolvedRequest(bool Resolved, string Comment, string? Description = null);

public sealed record AddCommentRequest(string Comment, string? Description = null);
