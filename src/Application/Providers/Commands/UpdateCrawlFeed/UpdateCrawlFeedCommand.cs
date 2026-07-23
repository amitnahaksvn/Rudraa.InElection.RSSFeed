using Mediator;
using Application.Abstractions;
using Application.Providers.Dtos;
using Domain.Entities;

namespace Application.Providers.Commands.UpdateCrawlFeed;

/// <summary>Full overwrite of an existing feed/endpoint's editable fields - the inline edit form on the Provider Management page. Pipeline/Provider/Country aren't editable here (moving a feed to a different provider/country/pipeline is a delete-and-recreate, not an edit).</summary>
public sealed record UpdateCrawlFeedCommand(
    string Id,
    string Name,
    string Url,
    string Category,
    string Language,
    bool Enabled,
    string? DefaultImageUrl,
    Dictionary<string, string>? QueryParameters) : IRequest<bool>;

public sealed class UpdateCrawlFeedCommandHandler : IRequestHandler<UpdateCrawlFeedCommand, bool>
{
    private readonly ICrawlFeedRepository _feeds;

    public UpdateCrawlFeedCommandHandler(ICrawlFeedRepository feeds)
    {
        _feeds = feeds;
    }

    public async ValueTask<bool> Handle(UpdateCrawlFeedCommand request, CancellationToken cancellationToken)
    {
        var existing = await _feeds.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var updated = new CrawlFeed
        {
            Id = request.Id,
            Pipeline = existing.Pipeline,
            Provider = existing.Provider,
            Country = existing.Country,
            Name = request.Name,
            Url = request.Url,
            Category = request.Category,
            Language = request.Language,
            Enabled = request.Enabled,
            DefaultImageUrl = request.DefaultImageUrl,
            QueryParameters = request.QueryParameters
        };

        return await _feeds.UpdateAsync(updated, cancellationToken);
    }
}
