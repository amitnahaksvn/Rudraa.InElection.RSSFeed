using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Commands.TriggerApiCrawl;

/// <summary>
/// Runs a JSON news-API crawl immediately, covering every enabled API provider. The
/// <see cref="Application.Crawl.Commands.TriggerCrawl.TriggerCrawlCommand"/> counterpart for the
/// API pipeline - subject to the same per-provider distributed lock (enforced inside
/// <see cref="INewsApiCrawlerService"/>), so a run already in progress is skipped rather than run
/// concurrently.
/// </summary>
public sealed record TriggerApiCrawlCommand : IRequest<CrawlHistoryDto>;

public sealed class TriggerApiCrawlCommandHandler : IRequestHandler<TriggerApiCrawlCommand, CrawlHistoryDto>
{
    private readonly INewsApiCrawlerService _crawlerService;

    public TriggerApiCrawlCommandHandler(INewsApiCrawlerService crawlerService)
    {
        _crawlerService = crawlerService;
    }

    public async ValueTask<CrawlHistoryDto> Handle(TriggerApiCrawlCommand request, CancellationToken cancellationToken)
    {
        var history = await _crawlerService.RunCrawlAsync(cancellationToken);
        return CrawlHistoryDto.FromDomain(history);
    }
}
