using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Application.Abstractions;
using Domain.Entities;
using ValidationException = Application.Common.Exceptions.ValidationException;

namespace WebPlatform.Infrastructure;

/// <summary>
/// Catches every otherwise-unhandled exception - including FluentValidation failures raised by
/// the mediator's ValidationBehaviour - and returns RFC7807 ProblemDetails instead of a raw 500.
/// A genuinely unexpected (500) exception is also persisted to <see cref="ErrorLog"/> with its
/// HTTP request context (path/method/query string/IP/user agent) - the same general error log
/// every crawl/API failure already goes into - so it surfaces in the next error-notification
/// dispatch email alongside everything else, instead of only ever living in the server log.
/// Expected 400s (validation, bad binding) are not recorded - they're not failures to alert on.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private static readonly string? EntryAssemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

    private readonly IHostEnvironment _environment;
    private readonly IErrorLogRepository _errorLogRepository;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    public ProblemDetailsExceptionHandler(
        IHostEnvironment environment, IErrorLogRepository errorLogRepository, ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _environment = environment;
        _errorLogRepository = errorLogRepository;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Instance = httpContext.Request.Path,
                Extensions = { ["errors"] = validationException.Errors }
            },
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request could not be bound to the endpoint's parameters.",
                Detail = badRequest.Message,
                Instance = httpContext.Request.Path
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                // The raw exception message can contain internal details (connection info, file
                // paths, library internals) that shouldn't reach an API client in production - the
                // full exception is already logged server-side just below regardless.
                Detail = _environment.IsDevelopment() ? exception.Message : null,
                Instance = httpContext.Request.Path
            }
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            await RecordErrorLogAsync(httpContext, exception, cancellationToken);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private async Task RecordErrorLogAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            await _errorLogRepository.InsertAsync(
                new ErrorLog
                {
                    ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException is { } inner ? $"{inner.GetType().FullName}: {inner.Message}" : null,
                    Source = "HTTP Request",
                    RequestPath = httpContext.Request.Path,
                    HttpMethod = httpContext.Request.Method,
                    QueryString = httpContext.Request.QueryString.HasValue ? httpContext.Request.QueryString.Value : null,
                    IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = httpContext.Request.Headers.UserAgent is { Count: > 0 } ua ? ua.ToString() : null,
                    Environment = _environment.EnvironmentName,
                    ApplicationName = _environment.ApplicationName,
                    ServiceName = _environment.ApplicationName,
                    MachineName = Environment.MachineName,
                    AssemblyVersion = EntryAssemblyVersion,
                    TraceId = httpContext.TraceIdentifier,
                    CreatedOn = DateTimeOffset.UtcNow,
                    IsSent = false
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Never let alert-recording itself break error handling - the response above already went out regardless.
            _logger.LogError(ex, "Failed to persist error log for unhandled HTTP exception");
        }
    }
}
