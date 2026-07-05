using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class SocialMediaSourceRepository : ISocialMediaSourceRepository
{
    private readonly IMongoCollection<SocialMediaSource> _collection;

    public SocialMediaSourceRepository(MongoDbContext context)
    {
        _collection = context.SocialMediaSources;
    }

    public async Task<IReadOnlyList<SocialMediaSource>> GetEnabledAsync(CancellationToken cancellationToken) =>
        await _collection.Find(s => s.Enabled).ToListAsync(cancellationToken);

    public async Task<SocialMediaSource?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _collection.Find(s => s.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<SocialMediaSource?> GetByPlatformAndIdentifierAsync(SocialPlatform platform, string identifier, CancellationToken cancellationToken) =>
        await _collection.Find(s => s.Platform == platform && s.Identifier == identifier).FirstOrDefaultAsync(cancellationToken);

    public async Task<string> InsertAsync(SocialMediaSource source, CancellationToken cancellationToken)
    {
        await _collection.InsertOneAsync(source, options: null, cancellationToken);
        return source.Id;
    }

    public Task UpdateLastPolledAtAsync(string id, DateTimeOffset lastPolledAt, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            s => s.Id == id,
            Builders<SocialMediaSource>.Update
                .Set(s => s.LastPolledAt, lastPolledAt)
                .Set(s => s.UpdatedOn, lastPolledAt),
            cancellationToken: cancellationToken);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<SocialMediaSource>>
        {
            // A channel is uniquely identified by (Platform, Identifier) - the same YouTube
            // channel id can't be registered twice, but the same Identifier value could
            // theoretically collide across different platforms (a Telegram handle happening to
            // match a YouTube channel id string), so the uniqueness is compound, not on
            // Identifier alone.
            new(Builders<SocialMediaSource>.IndexKeys.Ascending(s => s.Platform).Ascending(s => s.Identifier),
                new CreateIndexOptions { Name = "ux_socialmediasource_platform_identifier", Unique = true }),
            new(Builders<SocialMediaSource>.IndexKeys.Ascending(s => s.Enabled),
                new CreateIndexOptions { Name = "ix_socialmediasource_enabled" }),
            new(Builders<SocialMediaSource>.IndexKeys.Descending(s => s.Priority),
                new CreateIndexOptions { Name = "ix_socialmediasource_priority" }),
            new(Builders<SocialMediaSource>.IndexKeys.Ascending(s => s.Country),
                new CreateIndexOptions { Name = "ix_socialmediasource_country" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
