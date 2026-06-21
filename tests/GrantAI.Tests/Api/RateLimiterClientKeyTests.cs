using GrantAI.API.RateLimiting;
using Xunit;

namespace GrantAI.Tests.Api;

public class RateLimiterClientKeyTests
{
    [Theory]
    [InlineData("203.0.113.1", null, "203.0.113.1")]
    [InlineData("203.0.113.1, 10.0.0.1", null, "203.0.113.1")]
    [InlineData("  198.51.100.7  ", null, "198.51.100.7")]
    public void ForwardedFor_TakesPrecedenceOverRemoteIp(string forwarded, string? remote, string expected)
    {
        Assert.Equal(expected, RateLimiterExtensions.ResolveClientKey(forwarded, remote));
    }

    [Theory]
    [InlineData(null, "192.168.1.5", "192.168.1.5")]
    [InlineData("", "192.168.1.5", "192.168.1.5")]
    [InlineData("   ", "192.168.1.5", "192.168.1.5")]
    public void RemoteIp_UsedWhenForwardedForIsMissing(string? forwarded, string remote, string expected)
    {
        Assert.Equal(expected, RateLimiterExtensions.ResolveClientKey(forwarded, remote));
    }

    [Fact]
    public void Unknown_WhenNothingIsKnown()
    {
        Assert.Equal("unknown", RateLimiterExtensions.ResolveClientKey(null, null));
    }
}
