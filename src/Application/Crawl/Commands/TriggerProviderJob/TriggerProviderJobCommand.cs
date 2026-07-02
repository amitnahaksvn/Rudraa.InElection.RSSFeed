using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Commands.TriggerProviderJob;

/// <summary>
/// Triggers a single provider's recurring crawl job to run now, ahead of its own cron schedule.
/// Unlike <see cref="TriggerCrawl.TriggerCrawlCommand"/> (which runs a crawl synchronously
/// in-process, covering every enabled provider, and waits for the result), this only enqueues one
/// named provider's Hangfire job and returns immediately - execution happens asynchronously
/// wherever that job's server is running, guarded by the same distributed lock either way.
/// </summary>
public sealed record TriggerProviderJobCommand(string Provider) : IRequest<ProviderJobTriggeredDto>;

public sealed class TriggerProviderJobCommandHandler : IRequestHandler<TriggerProviderJobCommand, ProviderJobTriggeredDto>
{
    private readonly ICrawlJobTrigger _crawlJobTrigger;

    public TriggerProviderJobCommandHandler(ICrawlJobTrigger crawlJobTrigger)
    {
        _crawlJobTrigger = crawlJobTrigger;
    }

    public ValueTask<ProviderJobTriggeredDto> Handle(TriggerProviderJobCommand request, CancellationToken cancellationToken)
    {
        var jobId = _crawlJobTrigger.TriggerNow(request.Provider);
        return ValueTask.FromResult(new ProviderJobTriggeredDto(request.Provider, jobId));
    }
}
