using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Models;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Converts an <see cref="ErrorNotification"/> (the in-flight capture built at the moment a failure
/// happens) into a persisted <see cref="ErrorLog"/> row and inserts it - replacing the immediate
/// per-error/per-run email this codebase used to send. Errors are now recorded here and emailed
/// later, in a batch, by the error-notification dispatch job on its own schedule
/// (<c>ErrorNotificationDispatchService</c>) - so a burst of failures never floods the inbox and a
/// temporarily-down email pipeline never loses an error, only delays its notification. Used by
/// every crawler orchestrator (<see cref="NewsCrawlerOrchestrator"/>, <see cref="NewsApiCrawlerOrchestrator"/>)
/// and, directly, by <c>DynamicFeedIngestionService</c> for its single-error case - "never let a
/// failure recording itself fail the run" is cheap, defensive insurance the same way the email send
/// it replaces used to guarantee.
/// </summary>
public static class ErrorLogRecorder
{
    public static async Task RecordIfAnyAsync(
        IErrorLogRepository errorLogRepository,
        IReadOnlyList<ErrorNotification> errors,
        ILogger logger,
        string historyId,
        CancellationToken cancellationToken)
    {
        foreach (var error in errors)
        {
            await RecordAsync(errorLogRepository, error, logger, cancellationToken, historyId);
        }
    }

    public static Task RecordAsync(
        IErrorLogRepository errorLogRepository,
        ErrorNotification error,
        ILogger logger,
        CancellationToken cancellationToken) =>
        RecordAsync(errorLogRepository, error, logger, cancellationToken, historyId: null);

    private static async Task RecordAsync(
        IErrorLogRepository errorLogRepository,
        ErrorNotification error,
        ILogger logger,
        CancellationToken cancellationToken,
        string? historyId)
    {
        try
        {
            await errorLogRepository.InsertAsync(ToErrorLog(error), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[{RunId}] Failed to persist error log for {Operation}/{Provider}",
                historyId ?? error.CorrelationId, error.Operation, error.Provider);
        }
    }

    private static readonly string? EntryAssemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

    private static ErrorLog ToErrorLog(ErrorNotification n) => new()
    {
        ExceptionType = n.ExceptionType,
        Message = n.ErrorMessage,
        StackTrace = n.StackTrace,
        InnerException = n.InnerException,
        Source = n.Operation,
        ErrorCode = n.HttpStatusCode?.ToString(),
        Environment = n.Environment,
        ApplicationName = n.ApplicationName,
        ServiceName = n.ApplicationName,
        MachineName = System.Environment.MachineName,
        AssemblyVersion = EntryAssemblyVersion,
        TraceId = Activity.Current?.Id,
        CorrelationId = n.CorrelationId,
        HangfireJobId = n.HangfireJobId,
        Provider = n.Provider,
        FeedOrApiName = n.FeedOrApiName,
        Country = n.Country,
        SourceUrl = n.SourceUrl,
        HttpStatusCode = n.HttpStatusCode,
        ResponseBody = n.ResponseBody,
        ExecutionDuration = n.ExecutionDuration,
        CreatedOn = n.OccurredAt,
        IsSent = false
    };
}
