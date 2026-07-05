using Microsoft.Extensions.Logging;
using Application.Abstractions;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Seed;

/// <summary>
/// Idempotent Phase 1 seed for the <see cref="SocialMediaSource"/> collection - two YouTube
/// channels to prove out the pipeline end to end, the same "seeder exists solely to bootstrap the
/// first documents" reasoning as <see cref="FeedSourceSeeder"/>. Every later channel (more
/// politicians/parties, other platforms once they get a fetcher) is added the same way: a
/// document insert, not a code change.
///
/// Both channel ids were independently verified rather than guessed: Narendra Modi's
/// (<c>UC1NF71EwP41VdjAU1iXdLkw</c>) matches the channel already wired into the file-configured
/// YouTube provider under <c>NewsCrawler.appsettings.json</c> (India country block) - the exact
/// same channel is deliberately also seeded here to prove out the new Mongo-driven pipeline
/// end-to-end, which does mean it's now polled by both pipelines independently; harmless (the
/// usual Url-based dedup skips whichever copy arrives second) but wasteful, worth trimming to one
/// pipeline once this one's proven out. BJP's (<c>UCrwE8kVqtIUVUzKui2WVpuQ</c>) was not previously
/// wired in anywhere in this codebase and could only be corroborated via web search (its own
/// channel search result explicitly labeled "Bharatiya Janata Party - YouTube", description "BJP,
/// world's largest political party..."), not a live feed fetch/title check - this environment's
/// network policy blocks youtube.com outright, so unlike almost every other provider added to
/// this codebase, this one could not be curl/fetch-verified. Re-confirm once network access to
/// youtube.com is available.
/// </summary>
public sealed class SocialMediaSourceSeeder
{
    private readonly ISocialMediaSourceRepository _sourceRepository;
    private readonly SocialMediaSourceValidator _validator;
    private readonly ILogger<SocialMediaSourceSeeder> _logger;

    public SocialMediaSourceSeeder(ISocialMediaSourceRepository sourceRepository, SocialMediaSourceValidator validator, ILogger<SocialMediaSourceSeeder> logger)
    {
        _sourceRepository = sourceRepository;
        _validator = validator;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedOneAsync(
            new SocialMediaSource
            {
                Platform = SocialPlatform.YouTube,
                SourceType = SourceEntityType.Politician,
                Country = "India",
                Name = "Narendra Modi",
                Identifier = "UC1NF71EwP41VdjAU1iXdLkw",
                Handle = "@NarendraModi",
                Url = "https://www.youtube.com/channel/UC1NF71EwP41VdjAU1iXdLkw",
                Enabled = true,
                Priority = 1,
                PollIntervalMinutes = 30,
                TimeoutSeconds = 60,
                Language = "en",
                Category = "Video"
            },
            cancellationToken);

        await SeedOneAsync(
            new SocialMediaSource
            {
                Platform = SocialPlatform.YouTube,
                SourceType = SourceEntityType.Party,
                Country = "India",
                Name = "BJP",
                Identifier = "UCrwE8kVqtIUVUzKui2WVpuQ",
                Handle = "@BJP4India",
                Url = "https://www.youtube.com/channel/UCrwE8kVqtIUVUzKui2WVpuQ",
                Enabled = true,
                Priority = 1,
                PollIntervalMinutes = 30,
                TimeoutSeconds = 60,
                Language = "en",
                Category = "Video"
            },
            cancellationToken);
    }

    private async Task SeedOneAsync(SocialMediaSource source, CancellationToken cancellationToken)
    {
        var existing = await _sourceRepository.GetByPlatformAndIdentifierAsync(source.Platform, source.Identifier, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        source.CreatedOn = now;
        source.UpdatedOn = now;

        var validation = _validator.Validate(source);
        if (!validation.IsValid)
        {
            _logger.LogError("SocialMediaSource seed '{Name}' failed validation: {Errors}", source.Name, validation);
            return;
        }

        var id = await _sourceRepository.InsertAsync(source, cancellationToken);
        _logger.LogInformation("Seeded SocialMediaSource '{Platform}/{Name}' ({Id})", source.Platform, source.Name, id);
    }
}
