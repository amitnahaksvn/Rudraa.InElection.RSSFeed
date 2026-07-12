using MongoDB.Bson;
using MongoDB.Driver;
using Application.Abstractions;
using Application.Models;
using Domain.Entities;
using Infrastructure.Mongo;

namespace Infrastructure.Persistence;

public sealed class JobExecutionLogRepository : IJobExecutionLogRepository
{
    private readonly IMongoCollection<JobExecutionLog> _collection;

    public JobExecutionLogRepository(MongoDbContext context)
    {
        _collection = context.JobExecutionLogs;
    }

    public async Task InsertAsync(JobExecutionLog log, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(log.Id))
        {
            log.Id = ObjectId.GenerateNewId().ToString();
        }

        await _collection.InsertOneAsync(log, options: null, cancellationToken);
    }

    public Task UpdateAsync(JobExecutionLog log, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(l => l.Id == log.Id, log, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<JobExecutionLog>> GetFilteredAsync(JobExecutionLogFilter filter, CancellationToken cancellationToken)
    {
        var builder = Builders<JobExecutionLog>.Filter;
        var clauses = new List<FilterDefinition<JobExecutionLog>>();

        if (!string.IsNullOrWhiteSpace(filter.JobId))
        {
            clauses.Add(builder.Eq(l => l.JobId, filter.JobId));
        }

        if (filter.Status is { } status)
        {
            clauses.Add(builder.Eq(l => l.Status, status));
        }

        if (filter.From is { } from)
        {
            clauses.Add(builder.Gte(l => l.StartedAt, from));
        }

        if (filter.To is { } to)
        {
            clauses.Add(builder.Lte(l => l.StartedAt, to));
        }

        var combined = clauses.Count == 0 ? FilterDefinition<JobExecutionLog>.Empty : builder.And(clauses);

        return await _collection.Find(combined)
            .SortByDescending(l => l.StartedAt)
            .Skip(filter.Skip)
            .Limit(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var models = new[]
        {
            new CreateIndexModel<JobExecutionLog>(
                Builders<JobExecutionLog>.IndexKeys.Descending(l => l.StartedAt),
                new CreateIndexOptions { Name = "ix_jobexecutionlog_startedat" }),

            // Backs the job-report page's primary access pattern: "every execution of this one
            // job, newest first" - filtering on JobId first, then ranging/sorting on StartedAt,
            // matches this compound index's key order.
            new CreateIndexModel<JobExecutionLog>(
                Builders<JobExecutionLog>.IndexKeys.Ascending(l => l.JobId).Descending(l => l.StartedAt),
                new CreateIndexOptions { Name = "ix_jobexecutionlog_jobid_startedat" })
        };

        await _collection.Indexes.CreateManyAsync(models, cancellationToken: cancellationToken);
    }
}
