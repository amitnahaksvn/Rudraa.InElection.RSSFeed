using Cronos;
using FluentValidation;
using Microsoft.Extensions.Options;
using Application.Options;
using Domain.Enums;

namespace Application.Crawl.Commands.CreateOrUpdateRecurringJob;

public sealed class CreateOrUpdateRecurringJobCommandValidator : AbstractValidator<CreateOrUpdateRecurringJobCommand>
{
    public CreateOrUpdateRecurringJobCommandValidator(IOptions<NewsCrawlerOptions> rssOptions, IOptions<NewsApiCrawlerOptions> apiOptions)
    {
        var enabledRssProviders = rssOptions.Value.Countries
            .Where(c => c.Enabled)
            .SelectMany(c => c.Providers)
            .Where(p => p.Enabled)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enabledApiProviders = apiOptions.Value.Countries
            .Where(c => c.Enabled)
            .SelectMany(c => c.Providers)
            .Where(p => p.Enabled)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        RuleFor(c => c.JobName)
            .NotEmpty()
            .Must((command, name) =>
                (command.Pipeline == CrawlPipeline.Api ? enabledApiProviders : enabledRssProviders).Contains(name))
            .WithMessage(c => $"'{c.JobName}' is not an enabled {c.Pipeline} provider - configure it first.");

        RuleFor(c => c.Cron)
            .NotEmpty()
            .Must(BeAValidCronExpression)
            .WithMessage("Cron must be a valid standard 5-field cron expression, e.g. '*/5 * * * *'.");

        RuleFor(c => c.TimeZone)
            .NotEmpty()
            .Must(BeAValidTimeZone)
            .WithMessage(c => $"'{c.TimeZone}' is not a recognized time zone id (e.g. 'UTC', 'Asia/Kolkata').");
    }

    private static bool BeAValidCronExpression(string cron)
    {
        try
        {
            CronExpression.Parse(cron, CronFormat.Standard);
            return true;
        }
        catch (CronFormatException)
        {
            return false;
        }
    }

    private static bool BeAValidTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
