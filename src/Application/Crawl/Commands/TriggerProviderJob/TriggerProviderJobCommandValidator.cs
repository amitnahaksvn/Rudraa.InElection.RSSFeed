using FluentValidation;
using Microsoft.Extensions.Options;
using Application.Options;

namespace Application.Crawl.Commands.TriggerProviderJob;

public sealed class TriggerProviderJobCommandValidator : AbstractValidator<TriggerProviderJobCommand>
{
    public TriggerProviderJobCommandValidator(IOptions<NewsCrawlerOptions> options)
    {
        var triggerableProviders = options.Value.Providers
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Cron))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RuleFor(c => c.Provider)
            .NotEmpty()
            .Must(provider => triggerableProviders.Contains(provider))
            .WithMessage(c => $"'{c.Provider}' is not an enabled provider with a scheduled recurring job.");
    }
}
