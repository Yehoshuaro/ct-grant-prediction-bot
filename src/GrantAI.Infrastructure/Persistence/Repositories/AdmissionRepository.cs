using GrantAI.Application.Abstractions;
using GrantAI.Domain.Entities;
using GrantAI.Infrastructure.Persistence;
using MongoDB.Driver;

namespace GrantAI.Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB-backed <see cref="IAdmissionRepository"/>. Group codes are stored
/// upper-cased (see the row parser), so lookups upper-case the query value and
/// match it exactly.
/// </summary>
public sealed class AdmissionRepository : IAdmissionRepository
{
    private readonly IMongoCollection<AdmissionRecord> _collection;

    public AdmissionRepository(MongoContext context) => _collection = context.AdmissionRecords;

    public async Task<IReadOnlyList<AdmissionRecord>> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var filter = Builders<AdmissionRecord>.Filter.Eq(r => r.GroupCode, normalized);
        return await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetGroupCodesAsync(CancellationToken ct = default)
    {
        var codes = await _collection
            .Distinct(r => r.GroupCode, FilterDefinition<AdmissionRecord>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        codes.Sort(StringComparer.Ordinal);
        return codes;
    }

    public async Task<IReadOnlyList<AdmissionRecord>> GetAllAsync(CancellationToken ct = default)
        => await _collection.Find(FilterDefinition<AdmissionRecord>.Empty).ToListAsync(ct);

    public Task<long> CountAsync(CancellationToken ct = default)
        => _collection.CountDocumentsAsync(FilterDefinition<AdmissionRecord>.Empty, cancellationToken: ct);

    public async Task<IReadOnlyCollection<string>> GetExistingIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        var filter = Builders<AdmissionRecord>.Filter.In(r => r.Id, ids);
        return await _collection.Find(filter).Project(r => r.Id).ToListAsync(ct);
    }

    public async Task BulkUpsertAsync(IReadOnlyCollection<AdmissionRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;

        var writes = records.Select(record =>
        {
            var filter = Builders<AdmissionRecord>.Filter.Eq(r => r.Id, record.Id);
            return new ReplaceOneModel<AdmissionRecord>(filter, record) { IsUpsert = true };
        });

        await _collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }
}
