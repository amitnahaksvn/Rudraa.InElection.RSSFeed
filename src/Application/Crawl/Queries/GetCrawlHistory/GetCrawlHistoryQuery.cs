using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Queries.GetCrawlHistory;

public sealed record GetCrawlHistoryQuery(int Count) : IRequest<IReadOnlyList<CrawlHistoryDto>>;

public sealed class GetCrawlHistoryQueryHandler : IRequestHandler<GetCrawlHistoryQuery, IReadOnlyList<CrawlHistoryDto>>
{
    private readonly ICrawlHistoryRepository _history;

    public GetCrawlHistoryQueryHandler(ICrawlHistoryRepository history)
    {
        _history = history;
    }

    public async ValueTask<IReadOnlyList<CrawlHistoryDto>> Handle(GetCrawlHistoryQuery request, CancellationToken cancellationToken)
    {
        var history = await _history.GetRecentAsync(request.Count, cancellationToken);
        return history.Select(CrawlHistoryDto.FromDomain).ToList();
    }
}
