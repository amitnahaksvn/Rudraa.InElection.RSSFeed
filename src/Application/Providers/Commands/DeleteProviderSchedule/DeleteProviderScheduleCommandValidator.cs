using FluentValidation;
using Domain.Enums;

namespace Application.Providers.Commands.DeleteProviderSchedule;

public sealed class DeleteProviderScheduleCommandValidator : AbstractValidator<DeleteProviderScheduleCommand>
{
    public DeleteProviderScheduleCommandValidator()
    {
        RuleFor(c => c.Pipeline).Must(p => p is CrawlPipeline.Rss or CrawlPipeline.Api).WithMessage("Pipeline must be 'Rss' or 'Api'.");
        RuleFor(c => c.Provider).NotEmpty();
        RuleFor(c => c.Country).NotEmpty();
    }
}
