using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class ArticleFingerprintRepository : IArticleFingerprintRepository
{
    private readonly IMongoCollection<ArticleFingerprint> _collection;

    public ArticleFingerprintRepository(MongoDbContext context)
    {
        _collection = context.ArticleFingerprints;
    }

    public Task<ArticleFingerprint?> FindByUrlAsync(string url, CancellationToken cancellationToken) =>
        _collection.Find(f => f.Url == url).FirstOrDefaultAsync(cancellationToken)!;

    public Task<ArticleFingerprint?> FindByOriginalGuidAsync(string originalGuid, CancellationToken cancellationToken) =>
        _collection.Find(f => f.OriginalGuid == originalGuid).FirstOrDefaultAsync(cancellationToken)!;

    public Task<ArticleFingerprint?> FindByHashAsync(string hash, CancellationToken cancellationToken) =>
        _collection.Find(f => f.Hash == hash).FirstOrDefaultAsync(cancellationToken)!;

    public Task InsertAsync(ArticleFingerprint fingerprint, CancellationToken cancellationToken) =>
        _collection.InsertOneAsync(fingerprint, options: null, cancellationToken);

    public Task ReplaceAsync(ArticleFingerprint fingerprint, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(f => f.Id == fingerprint.Id, fingerprint, cancellationToken: cancellationToken);

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<ArticleFingerprint>>
        {
            new(Builders<ArticleFingerprint>.IndexKeys.Ascending(f => f.Url),
                new CreateIndexOptions { Unique = true, Name = "ux_articlefingerprint_url" }),
            new(Builders<ArticleFingerprint>.IndexKeys.Ascending(f => f.Hash),
                new CreateIndexOptions { Unique = true, Name = "ux_articlefingerprint_hash" }),
            new(Builders<ArticleFingerprint>.IndexKeys.Ascending(f => f.OriginalGuid),
                new CreateIndexOptions { Name = "ix_articlefingerprint_originalguid" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
