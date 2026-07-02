using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Queries.GetCrawlHistoryById;

public sealed record GetCrawlHistoryByIdQuery(string Id) : IRequest<CrawlHistoryDto?>;

public sealed class GetCrawlHistoryByIdQueryHandler : IRequestHandler<GetCrawlHistoryByIdQuery, CrawlHistoryDto?>
{
    private readonly ICrawlHistoryRepository _history;

    public GetCrawlHistoryByIdQueryHandler(ICrawlHistoryRepository history)
    {
        _history = history;
    }

    public async ValueTask<CrawlHistoryDto?> Handle(GetCrawlHistoryByIdQuery request, CancellationToken cancellationToken)
    {
        var history = await _history.GetByIdAsync(request.Id, cancellationToken);
        return history is null ? null : CrawlHistoryDto.FromDomain(history);
    }
}
