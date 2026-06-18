namespace GrantAI.Application.Abstractions;

/// <summary>
/// Distributed cache port (implemented with Redis in Infrastructure).
/// Exposes a cache-aside helper plus prefix invalidation so that an import
/// can wipe all derived/cached analytics in one call.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Cache-aside: return the cached value or invoke <paramref name="factory"/>,
    /// cache its result for <paramref name="ttl"/>, and return it.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Removes every key starting with <paramref name="prefix"/>.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
