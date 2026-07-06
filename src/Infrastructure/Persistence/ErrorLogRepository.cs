using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class ErrorLogRepository : IErrorLogRepository
{
    private readonly IMongoCollection<ErrorLog> _collection;

    public ErrorLogRepository(MongoDbContext context)
    {
        _collection = context.ErrorLogs;
    }

    public Task InsertAsync(ErrorLog errorLog, CancellationToken cancellationToken) =>
        _collection.InsertOneAsync(errorLog, options: null, cancellationToken);

    public async Task<IReadOnlyList<ErrorLog>> GetUnsentAsync(int limit, CancellationToken cancellationToken) =>
        await _collection
            .Find(e => !e.IsSent)
            .SortBy(e => e.CreatedOn)
            .Limit(limit)
            .ToListAsync(cancellationToken);

    public async Task MarkAsSentAsync(IReadOnlyList<string> ids, DateTimeOffset sentOn, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await _collection.UpdateManyAsync(
            Builders<ErrorLog>.Filter.In(e => e.Id, ids),
            Builders<ErrorLog>.Update.Set(e => e.IsSent, true).Set(e => e.SentOn, sentOn),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorLog>> GetPagedAsync(ErrorLogFilter filter, int skip, int limit, CancellationToken cancellationToken) =>
        await _collection
            .Find(BuildFilter(filter))
            .SortBy(e => e.IsResolved)
            .ThenByDescending(e => e.CreatedOn)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);

    public Task<long> CountAsync(ErrorLogFilter filter, CancellationToken cancellationToken) =>
        _collection.CountDocumentsAsync(BuildFilter(filter), cancellationToken: cancellationToken);

    public async Task<ErrorLog?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _collection.Find(e => e.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> SetResolvedAsync(string id, bool resolved, string comment, CancellationToken cancellationToken)
    {
        var entry = new ErrorLogHistoryEntry { Comment = comment, IsResolved = resolved, CreatedOn = DateTimeOffset.UtcNow };
        var update = Builders<ErrorLog>.Update
            .Set(e => e.IsResolved, resolved)
            .Set(e => e.ResolvedOn, resolved ? DateTimeOffset.UtcNow : null)
            .Push(e => e.History, entry);

        var result = await _collection.UpdateOneAsync(e => e.Id == id, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> AddCommentAsync(string id, string comment, CancellationToken cancellationToken)
    {
        // IsResolved isn't changing here - the history entry just needs to record what the row's
        // status already was at the moment of the comment, which means reading the current
        // document first rather than referencing "$IsResolved" via an aggregation-pipeline update
        // (simpler, and the rare race against a concurrent resolve/unresolve toggle is low-stakes
        // for an audit note).
        var existing = await _collection.Find(e => e.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var entry = new ErrorLogHistoryEntry { Comment = comment, IsResolved = existing.IsResolved, CreatedOn = DateTimeOffset.UtcNow };
        await _collection.UpdateOneAsync(
            e => e.Id == id,
            Builders<ErrorLog>.Update.Push(e => e.History, entry),
            cancellationToken: cancellationToken);
        return true;
    }

    // Every field here is an equality match except SearchText, a case-insensitive substring match
    // (via a Regex.Escape'd pattern, so a search term containing regex metacharacters can't turn
    // into an unintended pattern or a ReDoS risk) across the handful of fields an admin scanning
    // the error-monitor UI would actually type into a search box.
    private static FilterDefinition<ErrorLog> BuildFilter(ErrorLogFilter filter)
    {
        var builder = Builders<ErrorLog>.Filter;
        var clauses = new List<FilterDefinition<ErrorLog>>();

        if (filter.IsResolved is { } isResolved)
        {
            clauses.Add(builder.Eq(e => e.IsResolved, isResolved));
        }

        if (!string.IsNullOrWhiteSpace(filter.Provider))
        {
            clauses.Add(builder.Eq(e => e.Provider, filter.Provider));
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            clauses.Add(builder.Eq(e => e.Country, filter.Country));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            clauses.Add(builder.Eq(e => e.Source, filter.Source));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.SearchText), "i");
            clauses.Add(builder.Or(
                builder.Regex(e => e.Message, pattern),
                builder.Regex(e => e.ExceptionType, pattern),
                builder.Regex(e => e.Provider, pattern),
                builder.Regex(e => e.Source, pattern)));
        }

        return clauses.Count == 0 ? builder.Empty : builder.And(clauses);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new List<CreateIndexModel<ErrorLog>>
        {
            // Covers ErrorNotificationDispatchService's GetUnsentAsync query (filter IsSent=false,
            // sort by CreatedOn) directly from the index, without an in-memory sort.
            new(Builders<ErrorLog>.IndexKeys.Ascending(e => e.IsSent).Ascending(e => e.CreatedOn),
                new CreateIndexOptions { Name = "ix_errorlog_issent_createdon" }),
            new(Builders<ErrorLog>.IndexKeys.Descending(e => e.CreatedOn),
                new CreateIndexOptions { Name = "ix_errorlog_createdon" }),
            new(Builders<ErrorLog>.IndexKeys.Ascending(e => e.Provider),
                new CreateIndexOptions { Name = "ix_errorlog_provider" }),
            // Covers the error-monitor UI's default GetPagedAsync query/sort (unresolved first,
            // newest first within each group) directly from the index.
            new(Builders<ErrorLog>.IndexKeys.Ascending(e => e.IsResolved).Descending(e => e.CreatedOn),
                new CreateIndexOptions { Name = "ix_errorlog_isresolved_createdon" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
