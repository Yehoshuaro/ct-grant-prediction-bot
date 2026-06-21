using System.Collections.Concurrent;

namespace GrantAI.Bot.RateLimiting;

/// <summary>
/// Per-chat sliding-window throttle. Each chat keeps a list of recent request
/// timestamps in a dedicated bucket; an incoming call drops anything outside
/// the window and is accepted only if the surviving count stays under the
/// permit limit. Thread-safe via per-bucket locking.
///
/// In-memory is enough here: the bot runs as a single long-polling process,
/// so requests do not fan out to other instances.
/// </summary>
public sealed class BotRateLimiter
{
    private readonly ConcurrentDictionary<long, Bucket> _buckets = new();
    private readonly BotRateLimiterOptions _options;
    private readonly TimeProvider _clock;

    public BotRateLimiter(BotRateLimiterOptions options, TimeProvider? clock = null)
    {
        _options = options;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Records a new request for <paramref name="chatId"/> and reports whether
    /// it stays inside the limit.
    /// </summary>
    public bool TryAcquire(long chatId)
    {
        if (_options.PermitLimit <= 0 || _options.WindowSeconds <= 0)
            return true;

        var now = _clock.GetUtcNow();
        var window = TimeSpan.FromSeconds(_options.WindowSeconds);
        var cutoff = now - window;

        var bucket = _buckets.GetOrAdd(chatId, _ => new Bucket());
        lock (bucket)
        {
            // Drop timestamps older than the window. The list is naturally
            // append-ordered, so a single front-trim is enough.
            while (bucket.Hits.Count > 0 && bucket.Hits[0] <= cutoff)
            {
                bucket.Hits.RemoveAt(0);
            }

            if (bucket.Hits.Count >= _options.PermitLimit)
            {
                return false;
            }

            bucket.Hits.Add(now);
            return true;
        }
    }

    private sealed class Bucket
    {
        public List<DateTimeOffset> Hits { get; } = new(8);
    }
}
