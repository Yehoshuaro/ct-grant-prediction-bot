namespace GrantAI.API.RateLimiting;

/// <summary>
/// Configurable limits for the API's rate limiter. Two partitions: a relaxed
/// global one (reads) and a strict one (writes and expensive endpoints).
/// </summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    public PartitionLimits Global { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };

    public PartitionLimits Strict { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public sealed class PartitionLimits
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
        public int QueueLimit { get; set; }
    }
}
