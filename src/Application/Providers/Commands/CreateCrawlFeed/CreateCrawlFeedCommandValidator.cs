using FluentValidation;
using Application.Abstractions;
using Domain.Enums;

namespace Application.Providers.Commands.CreateCrawlFeed;

public sealed class CreateCrawlFeedCommandValidator : AbstractValidator<CreateCrawlFeedCommand>
{
    public CreateCrawlFeedCommandValidator(IProviderScheduleRepository schedules)
    {
        RuleFor(c => c.Pipeline).Must(p => p is CrawlPipeline.Rss or CrawlPipeline.Api).WithMessage("Pipeline must be 'Rss' or 'Api'.");
        RuleFor(c => c.Provider).NotEmpty();
        RuleFor(c => c.Country).NotEmpty();

        // A feed must belong to an already-existing provider-country schedule - the same reasoning
        // UpdateProviderScheduleCommandValidator already applies at the provider level, just one
        // level more specific now that a provider can have more than one country's own schedule.
        RuleFor(c => c)
            .MustAsync(async (c, cancellationToken) => await schedules.GetAsync(c.Pipeline, c.Provider, c.Country, cancellationToken) is not null)
            .WithMessage(c => $"'{c.Provider}' has no {c.Pipeline} schedule for country '{c.Country}' - add the provider-country schedule first.")
            .WithName("Country");

        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Url).NotEmpty();
        RuleFor(c => c.Category).NotEmpty();
        RuleFor(c => c.Language).NotEmpty();
    }
}
