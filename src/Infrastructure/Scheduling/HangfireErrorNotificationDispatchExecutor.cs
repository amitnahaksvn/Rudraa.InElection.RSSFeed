using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Services;

namespace Infrastructure.Scheduling;

/// <summary>
/// What the error-notification-dispatch recurring job actually invokes - same reasoning as
/// <see cref="HangfireRawResponseCleanupExecutor"/>: a friendly dashboard name and every log line
/// tagged with this specific job execution's id via <see cref="PerformContext"/>.
/// </summary>
public sealed class HangfireErrorNotificationDispatchExecutor
{
    private readonly IErrorNotificationDispatchService _dispatchService;
    private readonly JobExecutionLogger _executionLogger;
    private readonly ILogger<HangfireErrorNotificationDispatchExecutor> _logger;

    public HangfireErrorNotificationDispatchExecutor(
        IErrorNotificationDispatchService dispatchService, JobExecutionLogger executionLogger, ILogger<HangfireErrorNotificationDispatchExecutor> logger)
    {
        _dispatchService = dispatchService;
        _executionLogger = executionLogger;
        _logger = logger;
    }

    [JobDisplayName("Dispatch pending error notifications")]
    public async Task RunAsync(PerformContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["HangfireJobId"] = context.BackgroundJob.Id });

        await _executionLogger.RunAsync(
            HangfireJobIds.ErrorNotificationDispatch,
            "Dispatch pending error notifications",
            context.BackgroundJob.Id,
            () => _dispatchService.DispatchPendingAsync(cancellationToken),
            cancellationToken);
    }
}
