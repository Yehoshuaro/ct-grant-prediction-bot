using GrantAI.Application.Importing;
using GrantAI.Domain.Enums;
using Xunit;

namespace GrantAI.Tests.Importing;

public class ValueParsersTests
{
    [Theory]
    [InlineData("537", 537)]
    [InlineData("1 234", 1234)]      // thousands space
    [InlineData("1\u00A0234", 1234)] // non-breaking space
    [InlineData("91.09", 91)]        // rounded down
    [InlineData("0", 0)]
    public void ParseInt_ParsesMessyNumbers(string input, int expected)
        => Assert.Equal(expected, ValueParsers.ParseInt(input));

    [Theory]
    [InlineData("%")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("n/a")]
    [InlineData(null)]
    public void ParseInt_RejectsNonNumbers(string? input)
        => Assert.Null(ValueParsers.ParseInt(input));

    [Theory]
    [InlineData("85,5", 85.5)]   // decimal comma
    [InlineData("90", 90.0)]
    public void ParseDouble_HandlesCommaAndSpaces(string input, double expected)
        => Assert.Equal(expected, ValueParsers.ParseDouble(input)!.Value, precision: 6);

    [Theory]
    [InlineData("зима", Season.Winter)]
    [InlineData("лето", Season.Summer)]
    [InlineData("2024-зима-рус", Season.Winter)]
    [InlineData("winter", Season.Winter)]
    [InlineData("Summer", Season.Summer)]
    public void ParseSeason_RecognisesBothLanguages(string input, Season expected)
        => Assert.Equal(expected, ValueParsers.ParseSeason(input));

    [Fact]
    public void ParseSeason_ReturnsNullForUnknown()
        => Assert.Null(ValueParsers.ParseSeason("осень"));

    [Fact]
    public void TryParseCampaign_ReadsSheetName()
    {
        var campaign = ValueParsers.TryParseCampaign("2024-зима-рус");
        Assert.NotNull(campaign);
        Assert.Equal(2024, campaign!.Value.Year);
        Assert.Equal(Season.Winter, campaign.Value.Season);
    }

    [Fact]
    public void TryParseCampaign_ReadsTitleRow()
    {
        var campaign = ValueParsers.TryParseCampaign(
            "Статистические данные по итогам комплексного тестирования в магистратуру 2023 г. (лето)");
        Assert.NotNull(campaign);
        Assert.Equal(2023, campaign!.Value.Year);
        Assert.Equal(Season.Summer, campaign.Value.Season);
    }

    [Theory]
    [InlineData("нет года, только лето")]  // no year
    [InlineData("2025 без сезона")]         // no season
    public void TryParseCampaign_ReturnsNullWhenIncomplete(string input)
        => Assert.Null(ValueParsers.TryParseCampaign(input));
}
