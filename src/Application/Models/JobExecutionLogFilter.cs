using Domain.Enums;

namespace Application.Models;

/// <summary>Query shape for <see cref="Abstractions.IJobExecutionLogRepository.GetFilteredAsync"/> - every filter is optional/additive, same convention as <see cref="CrawlHistoryFilter"/>.</summary>
public sealed record JobExecutionLogFilter(
    string? JobId = null,
    JobExecutionStatus? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Skip = 0,
    int Take = 20);
