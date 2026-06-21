extern alias bot;

using bot::GrantAI.Bot.Handlers;
using Xunit;

namespace GrantAI.Tests.Bot;

public class CallbackDataTests
{
    [Theory]
    [InlineData("forecast", "M094", "forecast:M094")]
    [InlineData("history", "M001", "history:M001")]
    [InlineData("help", null, "help")]
    [InlineData("help", "", "help")]
    public void Build_ProducesExpectedPayload(string action, string? argument, string expected)
    {
        Assert.Equal(expected, CallbackData.Build(action, argument));
    }

    [Fact]
    public void Build_ReturnsNull_WhenArgumentMakesPayloadOversized()
    {
        var oversized = new string('A', 80);
        Assert.Null(CallbackData.Build("forecast", oversized));
    }

    [Theory]
    [InlineData("forecast:M094", "/forecast M094")]
    [InlineData("history:M001", "/history M001")]
    [InlineData("help", "/help")]
    [InlineData("start", "/start")]
    public void ToCommand_TurnsValidPayloadIntoCommandString(string data, string expected)
    {
        Assert.Equal(expected, CallbackData.ToCommand(data));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown:M094")]
    [InlineData("forecast:M094;DROP TABLE x")]
    [InlineData("forecast:<script>")]
    public void ToCommand_ReturnsNullForUnsafeOrUnknownPayloads(string? data)
    {
        Assert.Null(CallbackData.ToCommand(data));
    }

    [Fact]
    public void ToCommand_RejectsOversizedPayloads()
    {
        var data = "forecast:" + new string('A', 80);
        Assert.Null(CallbackData.ToCommand(data));
    }

    [Fact]
    public void ToCommand_AcceptsUnderscoresAndHyphensInArgument()
    {
        Assert.Equal("/forecast M-094_alt", CallbackData.ToCommand("forecast:M-094_alt"));
    }
}
