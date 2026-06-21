using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace GrantAI.API.Health;

/// <summary>
/// Pings Redis through the shared multiplexer. A <c>PING</c> returning "PONG"
/// (or simply succeeding) marks the service healthy; any exception or a closed
/// connection marks it unhealthy.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected) return HealthCheckResult.Unhealthy("Redis multiplexer is not connected");
            var latency = await _redis.GetDatabase().PingAsync().ConfigureAwait(false);
            return HealthCheckResult.Healthy($"Redis ping {latency.TotalMilliseconds:0} ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed", ex);
        }
    }
}
