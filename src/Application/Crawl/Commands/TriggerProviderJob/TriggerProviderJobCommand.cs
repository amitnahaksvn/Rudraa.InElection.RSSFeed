using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;
using Domain.Enums;

namespace Application.Crawl.Commands.TriggerProviderJob;

/// <summary>
/// Triggers a single provider's recurring crawl job to run now, ahead of its own cron schedule.
/// Unlike <see cref="TriggerCrawl.TriggerCrawlCommand"/>/<see cref="TriggerApiCrawl.TriggerApiCrawlCommand"/>
/// (which run a crawl synchronously in-process, covering every enabled provider for one pipeline,
/// and wait for the result), this only enqueues one named provider's Hangfire job and returns
/// immediately - execution happens asynchronously wherever that job's server is running, guarded
/// by the same distributed lock either way. <see cref="Pipeline"/> picks which provider list
/// (RSS or JSON-API) to validate/trigger against - RSS providers and API providers register under
/// different recurring-job ids (see <c>HangfireJobIds</c>), same as <see cref="ICrawlJobTrigger"/>
/// itself already requires.
/// </summary>
public sealed record TriggerProviderJobCommand(CrawlPipeline Pipeline, string Provider) : IRequest<ProviderJobTriggeredDto>;

public sealed class TriggerProviderJobCommandHandler : IRequestHandler<TriggerProviderJobCommand, ProviderJobTriggeredDto>
{
    private readonly ICrawlJobTrigger _crawlJobTrigger;

    public TriggerProviderJobCommandHandler(ICrawlJobTrigger crawlJobTrigger)
    {
        _crawlJobTrigger = crawlJobTrigger;
    }

    public ValueTask<ProviderJobTriggeredDto> Handle(TriggerProviderJobCommand request, CancellationToken cancellationToken)
    {
        var jobId = _crawlJobTrigger.TriggerNow(request.Pipeline, request.Provider);
        return ValueTask.FromResult(new ProviderJobTriggeredDto(request.Provider, jobId));
    }
}
