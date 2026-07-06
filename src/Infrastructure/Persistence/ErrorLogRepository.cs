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

    public async Task<bool> SetResolvedAsync(string id, bool resolved, string comment, string? description, CancellationToken cancellationToken)
    {
        var entry = new ErrorLogHistoryEntry { Comment = comment, Description = description, IsResolved = resolved, CreatedOn = DateTimeOffset.UtcNow };
        var update = Builders<ErrorLog>.Update
            .Set(e => e.IsResolved, resolved)
            .Set(e => e.ResolvedOn, resolved ? DateTimeOffset.UtcNow : null)
            .Push(e => e.History, entry);

        var result = await _collection.UpdateOneAsync(e => e.Id == id, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> AddCommentAsync(string id, string comment, string? description, CancellationToken cancellationToken)
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

        var entry = new ErrorLogHistoryEntry { Comment = comment, Description = description, IsResolved = existing.IsResolved, CreatedOn = DateTimeOffset.UtcNow };
        await _collection.UpdateOneAsync(
            e => e.Id == id,
            Builders<ErrorLog>.Update.Push(e => e.History, entry),
            cancellationToken: cancellationToken);
        return true;
    }

    // Every text field here is a case-insensitive substring match (via a Regex.Escape'd pattern,
    // so a value containing regex metacharacters can't turn into an unintended pattern or a ReDoS
    // risk) - Provider/Country/Source are free-text boxes in the UI, not exact-match dropdowns, so
    // an admin typing "aajtak" needs to match the stored "AajTak" the same way SearchText already did.
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
            clauses.Add(builder.Regex(e => e.Provider, CaseInsensitiveSubstring(filter.Provider)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            clauses.Add(builder.Regex(e => e.Country, CaseInsensitiveSubstring(filter.Country)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            clauses.Add(builder.Regex(e => e.Source, CaseInsensitiveSubstring(filter.Source)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var pattern = CaseInsensitiveSubstring(filter.SearchText);
            clauses.Add(builder.Or(
                builder.Regex(e => e.Message, pattern),
                builder.Regex(e => e.ExceptionType, pattern),
                builder.Regex(e => e.Provider, pattern),
                builder.Regex(e => e.Source, pattern)));
        }

        if (filter.Category is { } category)
        {
            clauses.Add(BuildCategoryFilter(builder, category));
        }

        return clauses.Count == 0 ? builder.Empty : builder.And(clauses);
    }

    private static BsonRegularExpression CaseInsensitiveSubstring(string value) => new(Regex.Escape(value), "i");

    // Rss/Api/Social/Http match the literal ErrorNotification.Operation strings every call site
    // uses for ErrorLog.Source (see IErrorLogRepository.ErrorLogCategory's own doc comment) -
    // Rss covers both RSS-proper and dynamic (Mongo-driven) feeds, since both are "a feed fetch
    // failed" from an admin's point of view. Critical/Warning are a severity derived from
    // HttpStatusCode since ErrorLog has no stored severity of its own: null (an unhandled
    // exception with no HTTP context at all) or 5xx is Critical, 4xx is Warning.
    private static FilterDefinition<ErrorLog> BuildCategoryFilter(FilterDefinitionBuilder<ErrorLog> builder, ErrorLogCategory category) => category switch
    {
        ErrorLogCategory.Rss => builder.In(e => e.Source, new[] { "RSS Feed Fetch", "Dynamic Feed Fetch" }),
        ErrorLogCategory.Api => builder.Eq(e => e.Source, "News API Fetch"),
        ErrorLogCategory.Social => builder.Eq(e => e.Source, "Social Media Fetch"),
        ErrorLogCategory.Http => builder.Eq(e => e.Source, "HTTP Request"),
        ErrorLogCategory.Critical => builder.Or(
            builder.Eq(e => e.HttpStatusCode, null),
            builder.Gte(e => e.HttpStatusCode, 500)),
        ErrorLogCategory.Warning => builder.And(
            builder.Gte(e => e.HttpStatusCode, 400),
            builder.Lt(e => e.HttpStatusCode, 500)),
        _ => builder.Empty,
    };

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
