using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Application.Abstractions;
using Application.Options;

namespace Application.Services;

/// <summary>
/// Runs on its own recurring schedule (<see cref="ErrorNotificationOptions.DispatchCron"/>, default
/// every 5 minutes) rather than at the moment an error occurs - <see cref="ErrorLogRecorder"/>
/// already persists every failure immediately, so this only has to fetch what's still unsent, email
/// it as one batch, and mark it sent. Marking as sent only happens once
/// <see cref="IEmailService.SendErrorLogBatchAsync"/> reports success, so a temporarily-down email
/// pipeline never loses an error - it just stays pending and gets retried on the next tick.
/// </summary>
public sealed class ErrorNotificationDispatchService : IErrorNotificationDispatchService
{
    private readonly IErrorLogRepository _errorLogRepository;
    private readonly IEmailService _emailService;
    private readonly ErrorNotificationOptions _options;
    private readonly ILogger<ErrorNotificationDispatchService> _logger;

    public ErrorNotificationDispatchService(
        IErrorLogRepository errorLogRepository,
        IEmailService emailService,
        IOptions<ErrorNotificationOptions> options,
        ILogger<ErrorNotificationDispatchService> logger)
    {
        _errorLogRepository = errorLogRepository;
        _emailService = emailService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await _errorLogRepository.GetUnsentAsync(_options.MaxBatchSize, cancellationToken);
        if (pending.Count == 0)
        {
            _logger.LogDebug("Error notification dispatch: no pending errors");
            return 0;
        }

        var sent = await _emailService.SendErrorLogBatchAsync(pending, cancellationToken);
        if (!sent)
        {
            _logger.LogWarning(
                "Error notification dispatch: failed to send batch email for {Count} pending error(s) - left unsent, will retry next tick",
                pending.Count);
            return 0;
        }

        var sentOn = DateTimeOffset.UtcNow;
        await _errorLogRepository.MarkAsSentAsync(pending.Select(e => e.Id).ToList(), sentOn, cancellationToken);

        _logger.LogInformation("Error notification dispatch: sent and marked {Count} pending error(s)", pending.Count);
        return pending.Count;
    }
}
