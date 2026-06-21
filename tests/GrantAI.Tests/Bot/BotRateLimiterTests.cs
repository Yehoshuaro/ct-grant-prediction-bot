extern alias bot;

using bot::GrantAI.Bot.RateLimiting;
using Xunit;

namespace GrantAI.Tests.Bot;

public class BotRateLimiterTests
{
    [Fact]
    public void TryAcquire_AcceptsUpToTheLimit()
    {
        var limiter = new BotRateLimiter(new BotRateLimiterOptions { PermitLimit = 3, WindowSeconds = 30 });

        Assert.True(limiter.TryAcquire(42));
        Assert.True(limiter.TryAcquire(42));
        Assert.True(limiter.TryAcquire(42));
        Assert.False(limiter.TryAcquire(42));
    }

    [Fact]
    public void TryAcquire_TracksChatsIndependently()
    {
        var limiter = new BotRateLimiter(new BotRateLimiterOptions { PermitLimit = 1, WindowSeconds = 30 });

        Assert.True(limiter.TryAcquire(1));
        Assert.False(limiter.TryAcquire(1));

        Assert.True(limiter.TryAcquire(2));
        Assert.False(limiter.TryAcquire(2));
    }

    [Fact]
    public void TryAcquire_AllowsAfterWindowPasses()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var limiter = new BotRateLimiter(
            new BotRateLimiterOptions { PermitLimit = 2, WindowSeconds = 10 }, clock);

        Assert.True(limiter.TryAcquire(7));
        Assert.True(limiter.TryAcquire(7));
        Assert.False(limiter.TryAcquire(7));

        clock.Advance(TimeSpan.FromSeconds(11));

        Assert.True(limiter.TryAcquire(7));
    }

    [Fact]
    public void TryAcquire_DisabledByZeroPermits()
    {
        var limiter = new BotRateLimiter(new BotRateLimiterOptions { PermitLimit = 0, WindowSeconds = 30 });
        for (var i = 0; i < 100; i++)
            Assert.True(limiter.TryAcquire(1));
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan delta) => _now += delta;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
