using GrantAI.Application.Abstractions;
using GrantAI.Domain.Entities;
using MongoDB.Driver;

namespace GrantAI.Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB-backed <see cref="IGrantCutoffRepository"/>. Group codes are stored
/// upper-cased (see the PDF parser), so lookups upper-case the query value and
/// match it exactly.
/// </summary>
public sealed class GrantCutoffRepository : IGrantCutoffRepository
{
    private readonly IMongoCollection<GrantCutoffRecord> _collection;

    public GrantCutoffRepository(MongoContext context) => _collection = context.GrantCutoffs;

    public async Task<IReadOnlyList<GrantCutoffRecord>> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var filter = Builders<GrantCutoffRecord>.Filter.Eq(r => r.GroupCode, normalized);
        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetGroupCodesAsync(CancellationToken ct = default)
    {
        var codes = await _collection
            .Distinct(r => r.GroupCode, FilterDefinition<GrantCutoffRecord>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        codes.Sort(StringComparer.Ordinal);
        return codes;
    }

    public async Task<IReadOnlyList<GrantCutoffRecord>> GetAllAsync(CancellationToken ct = default)
        => await _collection.Find(FilterDefinition<GrantCutoffRecord>.Empty).ToListAsync(ct);

    public Task<long> CountAsync(CancellationToken ct = default)
        => _collection.CountDocumentsAsync(FilterDefinition<GrantCutoffRecord>.Empty, cancellationToken: ct);

    public async Task BulkUpsertAsync(IReadOnlyCollection<GrantCutoffRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        var writes = records.Select(record =>
        {
            var filter = Builders<GrantCutoffRecord>.Filter.Eq(r => r.Id, record.Id);
            return new ReplaceOneModel<GrantCutoffRecord>(filter, record) { IsUpsert = true };
        });

        await _collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }
}
