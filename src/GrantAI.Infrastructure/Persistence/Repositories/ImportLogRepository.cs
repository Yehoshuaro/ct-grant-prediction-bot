using GrantAI.Application.Abstractions;
using GrantAI.Domain.Entities;
using GrantAI.Infrastructure.Persistence;
using MongoDB.Driver;

namespace GrantAI.Infrastructure.Persistence.Repositories;

/// <summary>MongoDB-backed <see cref="IImportLogRepository"/>.</summary>
public sealed class ImportLogRepository : IImportLogRepository
{
    private readonly IMongoCollection<ImportLog> _collection;

    public ImportLogRepository(MongoContext context) => _collection = context.ImportLogs;

    public Task AddAsync(ImportLog log, CancellationToken ct = default)
        => _collection.InsertOneAsync(log, cancellationToken: ct);

    public async Task<IReadOnlyList<ImportLog>> GetRecentAsync(int limit, CancellationToken ct = default)
        => await _collection
            .Find(FilterDefinition<ImportLog>.Empty)
            .SortByDescending(l => l.StartedAtUtc)
            .Limit(limit)
            .ToListAsync(ct).ConfigureAwait(false);
}
