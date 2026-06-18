using GrantAI.Domain.Entities;
using GrantAI.Infrastructure.Configuration;
using MongoDB.Driver;

namespace GrantAI.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper over <see cref="IMongoDatabase"/> exposing the strongly-typed
/// collections and a one-time index bootstrap. Registered as a singleton; the
/// underlying <see cref="IMongoClient"/> is thread-safe and pools connections.
/// </summary>
public sealed class MongoContext
{
    public const string AdmissionRecordsCollection = "admission_records";
    public const string ImportLogsCollection = "import_logs";

    private readonly IMongoDatabase _database;

    public MongoContext(IMongoClient client, MongoDbSettings settings)
    {
        MongoMappings.Register();
        _database = client.GetDatabase(settings.Database);
    }

    public IMongoCollection<AdmissionRecord> AdmissionRecords
        => _database.GetCollection<AdmissionRecord>(AdmissionRecordsCollection);

    public IMongoCollection<ImportLog> ImportLogs
        => _database.GetCollection<ImportLog>(ImportLogsCollection);

    /// <summary>
    /// Creates the indexes that back the main lookups (by group code and by
    /// campaign). Index creation is idempotent in MongoDB.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var keys = Builders<AdmissionRecord>.IndexKeys;

        var models = new[]
        {
            new CreateIndexModel<AdmissionRecord>(keys.Ascending(r => r.GroupCode)),
            new CreateIndexModel<AdmissionRecord>(
                keys.Ascending(r => r.Year).Ascending(r => r.Season))
        };

        await AdmissionRecords.Indexes.CreateManyAsync(models, ct);
    }
}
