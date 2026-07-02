using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Commands.TriggerCrawl;

/// <summary>
/// Runs a crawl immediately. Subject to the same distributed lock as the scheduled worker
/// (enforced inside <see cref="INewsCrawlerService"/>), so a run already in progress is skipped
/// rather than run concurrently.
/// </summary>
public sealed record TriggerCrawlCommand : IRequest<CrawlHistoryDto>;

public sealed class TriggerCrawlCommandHandler : IRequestHandler<TriggerCrawlCommand, CrawlHistoryDto>
{
    private readonly INewsCrawlerService _crawlerService;

    public TriggerCrawlCommandHandler(INewsCrawlerService crawlerService)
    {
        _crawlerService = crawlerService;
    }

    public async ValueTask<CrawlHistoryDto> Handle(TriggerCrawlCommand request, CancellationToken cancellationToken)
    {
        var history = await _crawlerService.RunCrawlAsync(cancellationToken);
        return CrawlHistoryDto.FromDomain(history);
    }
}
