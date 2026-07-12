namespace Domain.Enums;

/// <summary>Outcome of a single <see cref="Entities.JobExecutionLog"/> execution - a generic recurring-job run, distinct from a crawl run's own <see cref="CrawlStatus"/>.</summary>
public enum JobExecutionStatus
{
    Running,
    Succeeded,
    Failed
}
