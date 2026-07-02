namespace Application.Crawl.Dtos;

/// <summary>
/// Acknowledges that a provider's recurring crawl job was enqueued to run now - not the crawl
/// result itself, since execution happens asynchronously wherever that job's server is running.
/// </summary>
public sealed record ProviderJobTriggeredDto(string Provider, string JobId);
