using GrantAI.Application.Common;
using Xunit;

namespace GrantAI.Tests.Probability;

public class StatisticsTests
{
    [Fact]
    public void NormalCdf_AtZero_IsOneHalf()
        => Assert.Equal(0.5, Statistics.NormalCdf(0), precision: 6);

    [Theory]
    [InlineData(1.96, 0.975)]
    [InlineData(-1.96, 0.025)]
    [InlineData(1.0, 0.8413)]
    [InlineData(-1.0, 0.1587)]
    public void NormalCdf_MatchesKnownValues(double z, double expected)
        => Assert.Equal(expected, Statistics.NormalCdf(z), precision: 3);

    [Fact]
    public void NormalCdf_IsSymmetric()
    {
        for (var z = 0.0; z <= 3.0; z += 0.5)
        {
            Assert.Equal(1.0, Statistics.NormalCdf(z) + Statistics.NormalCdf(-z), precision: 6);
        }
    }

    [Fact]
    public void NormalCdf_IsMonotonicIncreasing()
    {
        var previous = Statistics.NormalCdf(-4);
        for (var z = -3.5; z <= 4.0; z += 0.5)
        {
            var current = Statistics.NormalCdf(z);
            Assert.True(current >= previous);
            previous = current;
        }
    }

    [Fact]
    public void Mean_ComputesArithmeticMean()
        => Assert.Equal(4.0, Statistics.Mean([2, 4, 6]), precision: 9);

    [Fact]
    public void Mean_EmptyReturnsZero()
        => Assert.Equal(0.0, Statistics.Mean([]));

    [Fact]
    public void SampleStdDev_UsesNMinusOne()
        // values {2,4,6}: variance = (4+0+4)/(3-1) = 4 -> stddev = 2
        => Assert.Equal(2.0, Statistics.SampleStdDev([2, 4, 6]), precision: 9);

    [Fact]
    public void SampleStdDev_FewerThanTwoValues_IsZero()
        => Assert.Equal(0.0, Statistics.SampleStdDev([5]));

    [Theory]
    [InlineData(5, 0, 10, 5)]
    [InlineData(-3, 0, 10, 0)]
    [InlineData(42, 0, 10, 10)]
    public void Clamp_BoundsValue(double value, double min, double max, double expected)
        => Assert.Equal(expected, Statistics.Clamp(value, min, max));
}
