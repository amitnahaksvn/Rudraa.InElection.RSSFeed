namespace Application.Providers.Dtos;

/// <summary>Read projection of one provider-country's live, database-backed schedule - returned after an edit from the Provider Management page.</summary>
public sealed record ProviderScheduleDto(string Pipeline, string Provider, string Country, bool Enabled, string Cron, string TimeZone);
