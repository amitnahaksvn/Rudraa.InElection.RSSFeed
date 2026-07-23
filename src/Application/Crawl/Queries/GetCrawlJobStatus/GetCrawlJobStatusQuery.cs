using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;
using Domain.Enums;

namespace Application.Crawl.Queries.GetCrawlJobStatus;

public sealed record GetCrawlJobStatusQuery(string Provider, string Country, CrawlPipeline Pipeline = CrawlPipeline.Rss) : IRequest<CrawlJobStatusDto?>;

public sealed class GetCrawlJobStatusQueryHandler : IRequestHandler<GetCrawlJobStatusQuery, CrawlJobStatusDto?>
{
    private readonly ICrawlJobStatusReader _statusReader;

    public GetCrawlJobStatusQueryHandler(ICrawlJobStatusReader statusReader)
    {
        _statusReader = statusReader;
    }

    public ValueTask<CrawlJobStatusDto?> Handle(GetCrawlJobStatusQuery request, CancellationToken cancellationToken)
    {
        var status = _statusReader.GetStatus(request.Pipeline, request.Provider, request.Country);
        return ValueTask.FromResult(status is null ? null : CrawlJobStatusDto.FromModel(status));
    }
}
