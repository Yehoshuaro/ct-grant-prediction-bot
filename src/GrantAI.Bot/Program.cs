using GrantAI.Application.DependencyInjection;
using GrantAI.Bot;
using GrantAI.Bot.Handlers;
using GrantAI.Infrastructure.DependencyInjection;
using GrantAI.Infrastructure.Logging;
using GrantAI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Telegram.Bot;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) =>
    {
        SerilogConfigurator.Configure(configuration);
        configuration.ReadFrom.Configuration(builder.Configuration);
    });

    // The token comes from the TELEGRAM_BOT_TOKEN environment variable (preferred,
    // matches docker-compose) or a Telegram:BotToken config entry for local runs.
    var token = builder.Configuration["TELEGRAM_BOT_TOKEN"]
                ?? builder.Configuration["Telegram:BotToken"];

    if (string.IsNullOrWhiteSpace(token))
    {
        Log.Fatal("TELEGRAM_BOT_TOKEN is not set. Provide it via environment variable or appsettings.json.");
        return 1;
    }

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(token));
    builder.Services.AddSingleton<CommandRouter>();
    builder.Services.AddHostedService<BotHostedService>();

    var host = builder.Build();

    // Best-effort index creation so the bot and API behave the same on a cold DB.
    using (var scope = host.Services.CreateScope())
    {
        try
        {
            await scope.ServiceProvider.GetRequiredService<MongoContext>().EnsureIndexesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not ensure MongoDB indexes on startup; continuing without them");
        }
    }

    Log.Information("GrantAI KZ bot starting");
    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "GrantAI KZ bot terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
