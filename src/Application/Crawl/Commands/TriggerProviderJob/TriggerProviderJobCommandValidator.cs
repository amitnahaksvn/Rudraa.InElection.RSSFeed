using FluentValidation;
using Microsoft.Extensions.Options;
using Application.Options;
using Domain.Enums;

namespace Application.Crawl.Commands.TriggerProviderJob;

public sealed class TriggerProviderJobCommandValidator : AbstractValidator<TriggerProviderJobCommand>
{
    public TriggerProviderJobCommandValidator(IOptions<NewsCrawlerOptions> rssOptions, IOptions<NewsApiCrawlerOptions> apiOptions)
    {
        var triggerableRssProviders = rssOptions.Value.Countries
            .Where(c => c.Enabled)
            .SelectMany(c => c.Providers)
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Cron))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var triggerableApiProviders = apiOptions.Value.Countries
            .Where(c => c.Enabled)
            .SelectMany(c => c.Providers)
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Cron))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RuleFor(c => c.Provider)
            .NotEmpty()
            .Must((command, provider) =>
                (command.Pipeline == CrawlPipeline.Api ? triggerableApiProviders : triggerableRssProviders).Contains(provider))
            .WithMessage(c => $"'{c.Provider}' is not an enabled {c.Pipeline} provider with a scheduled recurring job.");
    }
}
