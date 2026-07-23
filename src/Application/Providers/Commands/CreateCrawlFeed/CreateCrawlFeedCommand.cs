using Mediator;
using Application.Abstractions;
using Application.Providers.Dtos;
using Domain.Entities;
using Domain.Enums;

namespace Application.Providers.Commands.CreateCrawlFeed;

/// <summary>Adds a new RSS feed / JSON-API endpoint to an existing provider-country schedule - the "add feed" form on the Provider Management page.</summary>
public sealed record CreateCrawlFeedCommand(
    CrawlPipeline Pipeline,
    string Provider,
    string Country,
    string Name,
    string Url,
    string Category,
    string Language,
    bool Enabled,
    string? DefaultImageUrl,
    Dictionary<string, string>? QueryParameters) : IRequest<CrawlFeedDto>;

public sealed class CreateCrawlFeedCommandHandler : IRequestHandler<CreateCrawlFeedCommand, CrawlFeedDto>
{
    private readonly ICrawlFeedRepository _feeds;

    public CreateCrawlFeedCommandHandler(ICrawlFeedRepository feeds)
    {
        _feeds = feeds;
    }

    public async ValueTask<CrawlFeedDto> Handle(CreateCrawlFeedCommand request, CancellationToken cancellationToken)
    {
        var feed = new CrawlFeed
        {
            Pipeline = request.Pipeline,
            Provider = request.Provider,
            Country = request.Country,
            Name = request.Name,
            Url = request.Url,
            Category = request.Category,
            Language = request.Language,
            Enabled = request.Enabled,
            DefaultImageUrl = request.DefaultImageUrl,
            QueryParameters = request.QueryParameters
        };

        await _feeds.CreateAsync(feed, cancellationToken);
        return CrawlFeedDto.FromDomain(feed);
    }
}
