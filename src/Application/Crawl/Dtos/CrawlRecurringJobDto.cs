namespace Application.Crawl.Dtos;

/// <summary>The recurring crawl job that was just created/updated in Hangfire.</summary>
public sealed record CrawlRecurringJobDto(string JobId, string Provider, string Cron, string TimeZone);
