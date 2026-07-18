using System.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;

namespace WebPlatform.Infrastructure;

/// <summary>
/// First middleware in the pipeline - logs every inbound request's timestamp, full URL (including
/// query string), body (if any), elapsed time, and final status code.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var body = await ReadBodyAsync(context.Request);

        await _next(context);

        stopwatch.Stop();

        _logger.LogInformation(
            "{StartedAt:O} {Method} {Url} body={Body} responded {StatusCode} in {ElapsedMs}ms",
            startedAt, context.Request.Method, context.Request.GetDisplayUrl(), body,
            context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }

    // Buffers the body so it can be read here without consuming it for the actual endpoint handler.
    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return string.Empty;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }
}
