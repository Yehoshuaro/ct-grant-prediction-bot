using System.Text.Json;
using System.Text.Json.Serialization;
using GrantAI.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GrantAI.Infrastructure.Caching;

/// <summary>
/// Redis implementation of the cache port. Values are stored as JSON. Prefix
/// invalidation uses SCAN (via <see cref="IServer.KeysAsync"/>) so an import can
/// drop every derived value under <c>grantai:</c> in one call.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var (found, value) = await TryGetAsync<T>(key);
        return found ? value : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await Db.StringSetAsync(key, json, ttl);
    }

    public async Task<T> GetOrSetAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        var (found, cached) = await TryGetAsync<T>(key);
        if (found && cached is not null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return cached;
        }

        _logger.LogDebug("Cache miss for {Key}", key);
        var fresh = await factory(ct);
        if (fresh is not null)
            await SetAsync(key, fresh, ttl, ct);

        return fresh;
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var pattern = prefix + "*";
        var removed = 0;

        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica)
                continue;

            await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(ct))
            {
                await Db.KeyDeleteAsync(key);
                removed++;
            }
        }

        _logger.LogInformation("Invalidated {Count} cache entries matching {Pattern}", removed, pattern);
    }

    private IDatabase Db => _redis.GetDatabase();

    private async Task<(bool Found, T? Value)> TryGetAsync<T>(string key)
    {
        try
        {
            var raw = await Db.StringGetAsync(key);
            if (raw.IsNullOrEmpty)
                return (false, default);

            return (true, JsonSerializer.Deserialize<T>(raw.ToString(), JsonOptions));
        }
        catch (Exception ex)
        {
            // A cache problem must never break a request: log and fall through.
            _logger.LogWarning(ex, "Cache read failed for {Key}; treating as a miss", key);
            return (false, default);
        }
    }
}
