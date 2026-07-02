using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Commands.CreateOrUpdateRecurringJob;

/// <summary>
/// Creates (or updates, if it already exists) a provider's recurring crawl job - job name is the
/// provider name, since every job this API can create does exactly one fixed, safe thing (crawl
/// that provider) rather than arbitrary caller-supplied code. This is a live override of
/// NewsCrawler.appsettings.json's Cron for that provider - see <see cref="ICrawlJobTrigger.CreateOrUpdate"/>
/// for how long it actually lasts.
/// </summary>
public sealed record CreateOrUpdateRecurringJobCommand(string JobName, string Cron, string TimeZone = "UTC")
    : IRequest<CrawlRecurringJobDto>;

public sealed class CreateOrUpdateRecurringJobCommandHandler
    : IRequestHandler<CreateOrUpdateRecurringJobCommand, CrawlRecurringJobDto>
{
    private readonly ICrawlJobTrigger _crawlJobTrigger;

    public CreateOrUpdateRecurringJobCommandHandler(ICrawlJobTrigger crawlJobTrigger)
    {
        _crawlJobTrigger = crawlJobTrigger;
    }

    public ValueTask<CrawlRecurringJobDto> Handle(CreateOrUpdateRecurringJobCommand request, CancellationToken cancellationToken)
    {
        var jobId = _crawlJobTrigger.CreateOrUpdate(request.JobName, request.Cron, request.TimeZone);
        return ValueTask.FromResult(new CrawlRecurringJobDto(jobId, request.JobName, request.Cron, request.TimeZone));
    }
}
