extern alias bot;

using bot::GrantAI.Bot.Handlers;
using GrantAI.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrantAI.Tests.Bot;

public class CommandRouterTests
{
    private static CommandRouter NewRouter()
        => new(new FakeSpecialtyQueryService(), new FakeGrantQueryService(),
            NullLogger<CommandRouter>.Instance);

    [Fact]
    public async Task Help_ReturnsTextAndKeyboard()
    {
        var reply = await NewRouter().RouteAsync("/help", default);
        Assert.NotNull(reply.Keyboard);
        Assert.Contains("Команды", reply.Text);
    }

    [Fact]
    public async Task Start_ReturnsTextAndKeyboard()
    {
        var reply = await NewRouter().RouteAsync("/start", default);
        Assert.NotNull(reply.Keyboard);
    }

    [Fact]
    public async Task Speciality_KnownCode_AttachesPerCodeKeyboard()
    {
        var reply = await NewRouter().RouteAsync($"/speciality {FakeSpecialtyQueryService.KnownCode}", default);
        Assert.NotNull(reply.Keyboard);
    }

    [Fact]
    public async Task Speciality_UnknownCode_ReplyHasNoKeyboard()
    {
        var reply = await NewRouter().RouteAsync("/speciality ZZZ999", default);
        Assert.Null(reply.Keyboard);
        Assert.Contains("Нет данных", reply.Text);
    }

    [Fact]
    public async Task Forecast_UnknownCode_ReportsNotFound()
    {
        var reply = await NewRouter().RouteAsync("/forecast ZZZ999", default);
        Assert.Null(reply.Keyboard);
    }

    [Fact]
    public async Task Forecast_NoArgument_ReturnsUsage()
    {
        var reply = await NewRouter().RouteAsync("/forecast", default);
        Assert.Null(reply.Keyboard);
        Assert.Contains("Использование", reply.Text);
    }

    [Fact]
    public async Task Grant_KnownCode_AttachesKeyboard()
    {
        var reply = await NewRouter().RouteAsync($"/grant {FakeGrantQueryService.KnownCode}", default);
        Assert.NotNull(reply.Keyboard);
    }

    [Fact]
    public async Task UnknownCommand_FallsBackToHelp()
    {
        var reply = await NewRouter().RouteAsync("/totally-unknown M094", default);
        Assert.NotNull(reply.Keyboard);
        Assert.Contains("Команды", reply.Text);
    }
}
