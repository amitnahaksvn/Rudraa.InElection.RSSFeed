using Mediator;
using Application.Abstractions;
using Application.Crawl.Dtos;

namespace Application.Crawl.Queries.GetCrawlJobStatus;

public sealed record GetCrawlJobStatusQuery(string Provider) : IRequest<CrawlJobStatusDto?>;

public sealed class GetCrawlJobStatusQueryHandler : IRequestHandler<GetCrawlJobStatusQuery, CrawlJobStatusDto?>
{
    private readonly ICrawlJobStatusReader _statusReader;

    public GetCrawlJobStatusQueryHandler(ICrawlJobStatusReader statusReader)
    {
        _statusReader = statusReader;
    }

    public ValueTask<CrawlJobStatusDto?> Handle(GetCrawlJobStatusQuery request, CancellationToken cancellationToken)
    {
        var status = _statusReader.GetStatus(request.Provider);
        return ValueTask.FromResult(status is null ? null : CrawlJobStatusDto.FromModel(status));
    }
}
