using GrantAI.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GrantAI.API.Health;

/// <summary>
/// Pings MongoDB with <c>{ ping: 1 }</c>. Lightweight, no external dependency.
/// Marked <see cref="HealthStatus.Unhealthy"/> on failure so readiness probes
/// can keep the API out of rotation until Mongo is reachable again.
/// </summary>
public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly MongoContext _context;
    private readonly IMongoClient _client;

    public MongoHealthCheck(MongoContext context, IMongoClient client)
    {
        _context = context;
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the AdmissionRecords database so we exercise the actual
            // database we will read/write against, not just the admin DB.
            var db = _client.GetDatabase(_context.AdmissionRecords.Database.DatabaseNamespace.DatabaseName);
            await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB ping failed", ex);
        }
    }
}
