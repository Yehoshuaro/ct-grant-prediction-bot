namespace GrantAI.Bot.RateLimiting;

/// <summary>
/// Per-chat throttle limits for the bot. Bound from the "BotRateLimit" config
/// section. Defaults: 20 commands per 30 seconds, which is generous for a real
/// user but cheap to block obvious spam.
/// </summary>
public sealed class BotRateLimiterOptions
{
    public const string SectionName = "BotRateLimit";

    public int PermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 30;
}
