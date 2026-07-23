using Mediator;
using Application.Abstractions;
using Domain.Enums;

namespace Application.Providers.Commands.DeleteProviderSchedule;

/// <summary>Deletes one provider-country's catalog record entirely and removes its live Hangfire recurring job - unlike disabling (which keeps the record but stops scheduling it), this removes the row itself. Its feeds are left orphaned in <c>CrawlFeeds</c> (same reasoning as <c>DeleteCountryCommand</c> not cascading) - they simply stop being fetched since nothing references them by provider-country anymore.</summary>
public sealed record DeleteProviderScheduleCommand(CrawlPipeline Pipeline, string Provider, string Country) : IRequest<bool>;

public sealed class DeleteProviderScheduleCommandHandler : IRequestHandler<DeleteProviderScheduleCommand, bool>
{
    private readonly IProviderScheduleRepository _schedules;
    private readonly ICrawlJobTrigger _jobTrigger;

    public DeleteProviderScheduleCommandHandler(IProviderScheduleRepository schedules, ICrawlJobTrigger jobTrigger)
    {
        _schedules = schedules;
        _jobTrigger = jobTrigger;
    }

    public async ValueTask<bool> Handle(DeleteProviderScheduleCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _schedules.DeleteAsync(request.Pipeline, request.Provider, request.Country, cancellationToken);
        if (deleted)
        {
            _jobTrigger.Remove(request.Pipeline, request.Provider, request.Country);
        }

        return deleted;
    }
}
