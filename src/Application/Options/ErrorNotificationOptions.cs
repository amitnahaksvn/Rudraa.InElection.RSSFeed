using System.ComponentModel.DataAnnotations;

namespace Application.Options;

/// <summary>
/// Root configuration section ("ErrorNotification") controlling the recurring job that batches
/// every not-yet-emailed <c>ErrorLog</c> row into one summary email - see
/// <c>Application.Services.ErrorNotificationDispatchService</c>. Errors themselves are persisted
/// immediately wherever they occur (crawl failures, dynamic feed failures, unhandled HTTP request
/// exceptions); this section only controls how often the batch email goes out and how large one
/// batch is allowed to get.
/// </summary>
public sealed class ErrorNotificationOptions
{
    public const string SectionName = "ErrorNotification";

    /// <summary>Standard 5-field cron expression for the recurring dispatch job. Default: every 5 minutes.</summary>
    [Required]
    public string DispatchCron { get; set; } = "*/5 * * * *";

    /// <summary>
    /// Max number of pending (<c>IsSent</c>=false) errors included in a single dispatch email -
    /// keeps one email a reasonable size when a burst of failures piles up faster than the
    /// dispatch cron fires; any remainder is simply picked up on the next tick.
    /// </summary>
    [Range(1, 1000)]
    public int MaxBatchSize { get; set; } = 100;
}
