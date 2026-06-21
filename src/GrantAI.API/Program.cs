using System.Text.Json.Serialization;
using GrantAI.API.RateLimiting;
using GrantAI.Application.DependencyInjection;
using GrantAI.Infrastructure.DependencyInjection;
using GrantAI.Infrastructure.Logging;
using GrantAI.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;
using Serilog;

// Bootstrap a minimal logger first so failures during host construction are
// still captured; it is replaced by the configured logger once the host builds.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        SerilogConfigurator.Configure(configuration);
        configuration.ReadFrom.Configuration(context.Configuration);
    });

    // Application use-cases + Infrastructure adapters (Mongo, Redis, Excel).
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var rateLimitSettings = builder.Configuration.GetSection(RateLimitSettings.SectionName)
        .Get<RateLimitSettings>() ?? new RateLimitSettings();
    builder.Services.AddGrantAiRateLimiter(rateLimitSettings);

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            // Serialise enums as their names ("Summer", "ScientificPedagogical")
            // so API and bot payloads are self-describing.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "GrantAI KZ API",
            Version = "v1",
            Description =
                "Analytics, forecasting and grant-probability API over Kazakhstan " +
                "master's-degree admission statistics. Import Excel campaign files and " +
                "query history, forecasts and admission chances by educational program group."
        });

        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    });

    var app = builder.Build();

    // Create MongoDB indexes on startup. A failure here (e.g. Mongo not ready
    // yet) is logged but must not stop the API from starting; the indexes are
    // an optimisation, not a correctness requirement.
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var mongo = scope.ServiceProvider.GetRequiredService<MongoContext>();
            await mongo.EnsureIndexesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not ensure MongoDB indexes on startup; continuing without them");
        }
    }

    app.UseSerilogRequestLogging();

    // Swagger is enabled in every environment: this is a portfolio/demo API and
    // the UI doubles as its primary documentation surface.
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GrantAI KZ API v1");
        options.DocumentTitle = "GrantAI KZ API";
    });

    app.UseRateLimiter();

    app.MapControllers();

    Log.Information("GrantAI KZ API starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GrantAI KZ API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Exposed so the integration test project can spin the API up through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
