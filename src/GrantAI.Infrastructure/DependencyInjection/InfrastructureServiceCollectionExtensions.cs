using GrantAI.Application.Abstractions;
using GrantAI.Application.Common;
using GrantAI.Application.Importing;
using GrantAI.Application.Importing.Grants;
using GrantAI.Infrastructure.Caching;
using GrantAI.Infrastructure.Configuration;
using GrantAI.Infrastructure.Excel;
using GrantAI.Infrastructure.Pdf;
using GrantAI.Infrastructure.Persistence;
using GrantAI.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;

namespace GrantAI.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the Infrastructure adapters: MongoDB (client, context, repositories),
/// Redis (multiplexer + cache), the ClosedXML workbook reader, and the strongly
/// typed settings bound from configuration. All components are singletons because
/// the MongoDB client and the Redis multiplexer are thread-safe and intended to
/// be shared for the lifetime of the process.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
                            ?? new MongoDbSettings();
        var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>()
                            ?? new RedisSettings();
        var cacheSettings = configuration.GetSection("Cache").Get<CacheSettings>()
                            ?? new CacheSettings();

        services.AddSingleton(mongoSettings);
        services.AddSingleton(redisSettings);
        services.AddSingleton(cacheSettings);

        // MongoDB
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
        services.AddSingleton<MongoContext>();
        services.AddSingleton<IAdmissionRepository, AdmissionRepository>();
        services.AddSingleton<IImportLogRepository, ImportLogRepository>();
        services.AddSingleton<IGrantCutoffRepository, GrantCutoffRepository>();

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisSettings.ConnectionString));
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Excel
        services.AddSingleton<IWorkbookReader, ClosedXmlWorkbookReader>();

        // Grant PDFs
        services.AddSingleton<IGrantPdfReader, PdfPigGrantPdfReader>();

        return services;
    }
}
