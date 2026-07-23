using FluentValidation;
using Application.Abstractions;
using Domain.Enums;

namespace Application.Crawl.Commands.TriggerProviderJob;

public sealed class TriggerProviderJobCommandValidator : AbstractValidator<TriggerProviderJobCommand>
{
    public TriggerProviderJobCommandValidator(ICrawlCountryRepository countries, IProviderScheduleRepository schedules)
    {
        RuleFor(c => c.Provider).NotEmpty();
        RuleFor(c => c.Country).NotEmpty();

        RuleFor(c => c)
            .MustAsync(async (command, cancellationToken) =>
            {
                var schedule = await schedules.GetAsync(command.Pipeline, command.Provider, command.Country, cancellationToken);
                if (schedule is null || !schedule.Enabled || string.IsNullOrWhiteSpace(schedule.Cron))
                {
                    return false;
                }

                var country = await countries.GetByNameAsync(command.Pipeline, schedule.Country, cancellationToken);
                return country is { Enabled: true };
            })
            .WithMessage(c => $"'{c.Provider}' ({c.Country}) is not an enabled {c.Pipeline} provider-country with a scheduled recurring job.")
            .WithName("Provider");
    }
}
