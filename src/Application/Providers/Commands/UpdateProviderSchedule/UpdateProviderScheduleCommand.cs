using Mediator;
using Application.Abstractions;
using Application.Providers.Dtos;
using Domain.Entities;
using Domain.Enums;

namespace Application.Providers.Commands.UpdateProviderSchedule;

/// <summary>
/// Creates a brand-new provider or fully overwrites an existing one's catalog record - the
/// user-driven add/edit path from the Provider Management page (the enable/disable toggle, cron
/// editor, and the "add provider" form all funnel through this one command, since
/// <see cref="IProviderScheduleRepository.UpsertAsync"/> already handles insert-or-overwrite).
/// Persists to <see cref="ProviderSchedule"/> and immediately updates the live Hangfire recurring
/// job - enabling registers/reschedules it via <see cref="ICrawlJobTrigger.CreateOrUpdate"/>,
/// disabling removes it via <see cref="ICrawlJobTrigger.Remove"/>, so the effect is instant.
/// <see cref="BaseUrl"/>/<see cref="AuthType"/>/<see cref="AuthParamName"/>/<see cref="TimeoutSeconds"/>
/// only apply to <see cref="CrawlPipeline.Api"/> providers; <see cref="SaveRawResponses"/> only to
/// <see cref="CrawlPipeline.Rss"/> ones - the validator only requires whichever half is relevant
/// to the given <see cref="Pipeline"/>.
/// </summary>
public sealed record UpdateProviderScheduleCommand(
    CrawlPipeline Pipeline,
    string Provider,
    string Country,
    bool Enabled,
    string Cron,
    string TimeZone = "UTC",
    bool SaveRawResponses = true,
    string? BaseUrl = null,
    ApiAuthType? AuthType = null,
    string? AuthParamName = null,
    int? TimeoutSeconds = null) : IRequest<ProviderScheduleDto>;

public sealed class UpdateProviderScheduleCommandHandler : IRequestHandler<UpdateProviderScheduleCommand, ProviderScheduleDto>
{
    private readonly IProviderScheduleRepository _schedules;
    private readonly ICrawlJobTrigger _jobTrigger;

    public UpdateProviderScheduleCommandHandler(IProviderScheduleRepository schedules, ICrawlJobTrigger jobTrigger)
    {
        _schedules = schedules;
        _jobTrigger = jobTrigger;
    }

    public async ValueTask<ProviderScheduleDto> Handle(UpdateProviderScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = new ProviderSchedule
        {
            Pipeline = request.Pipeline,
            Provider = request.Provider,
            Country = request.Country,
            Enabled = request.Enabled,
            Cron = request.Cron,
            TimeZone = request.TimeZone,
            SaveRawResponses = request.SaveRawResponses,
            BaseUrl = request.BaseUrl,
            AuthType = request.AuthType,
            AuthParamName = request.AuthParamName,
            TimeoutSeconds = request.TimeoutSeconds,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _schedules.UpsertAsync(schedule, cancellationToken);

        if (request.Enabled)
        {
            _jobTrigger.CreateOrUpdate(request.Pipeline, request.Provider, request.Country, request.Cron, request.TimeZone);
        }
        else
        {
            _jobTrigger.Remove(request.Pipeline, request.Provider, request.Country);
        }

        return new ProviderScheduleDto(request.Pipeline.ToString(), request.Provider, request.Country, request.Enabled, request.Cron, request.TimeZone);
    }
}
