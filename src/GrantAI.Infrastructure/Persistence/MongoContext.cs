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
    public const string GrantCutoffsCollection = "grant_cutoffs";

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

    public IMongoCollection<GrantCutoffRecord> GrantCutoffs
        => _database.GetCollection<GrantCutoffRecord>(GrantCutoffsCollection);

    /// <summary>
    /// Creates the indexes that back the main lookups (by group code and by
    /// campaign / intake year). Index creation is idempotent in MongoDB.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var admissionKeys = Builders<AdmissionRecord>.IndexKeys;
        var admissionModels = new[]
        {
            new CreateIndexModel<AdmissionRecord>(admissionKeys.Ascending(r => r.GroupCode)),
            new CreateIndexModel<AdmissionRecord>(
                admissionKeys.Ascending(r => r.Year).Ascending(r => r.Season))
        };
        await AdmissionRecords.Indexes.CreateManyAsync(admissionModels, ct);

        var grantKeys = Builders<GrantCutoffRecord>.IndexKeys;
        var grantModels = new[]
        {
            new CreateIndexModel<GrantCutoffRecord>(grantKeys.Ascending(r => r.GroupCode)),
            new CreateIndexModel<GrantCutoffRecord>(
                grantKeys.Ascending(r => r.Year).Ascending(r => r.GroupCode))
        };
        await GrantCutoffs.Indexes.CreateManyAsync(grantModels, ct);
    }
}
