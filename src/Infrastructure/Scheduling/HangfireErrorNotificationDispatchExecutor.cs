using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Application.Abstractions;

namespace Infrastructure.Scheduling;

/// <summary>
/// What the error-notification-dispatch recurring job actually invokes - same reasoning as every
/// other Hangfire executor in this file: a friendly dashboard name and every log line tagged with
/// this specific job execution's id via <see cref="PerformContext"/>.
/// </summary>
public sealed class HangfireErrorNotificationDispatchExecutor
{
    private readonly IErrorNotificationDispatchService _dispatchService;
    private readonly ILogger<HangfireErrorNotificationDispatchExecutor> _logger;

    public HangfireErrorNotificationDispatchExecutor(
        IErrorNotificationDispatchService dispatchService, ILogger<HangfireErrorNotificationDispatchExecutor> logger)
    {
        _dispatchService = dispatchService;
        _logger = logger;
    }

    [JobDisplayName("Dispatch pending error notifications")]
    public async Task RunAsync(PerformContext context, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["HangfireJobId"] = context.BackgroundJob.Id });

        await _dispatchService.DispatchPendingAsync(cancellationToken);
    }
}
