using FluentValidation;
using GrantAI.Application.Analytics;
using GrantAI.Application.Forecasting;
using GrantAI.Application.Importing;
using GrantAI.Application.Importing.Grants;
using GrantAI.Application.Probability;
using GrantAI.Application.Specialties;
using GrantAI.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace GrantAI.Application.DependencyInjection;

/// <summary>
/// Registers the Application layer: AutoMapper, FluentValidation, the pure
/// engines and the read/import orchestrators. Everything is stateless, so the
/// services are registered as singletons (which also avoids captive-dependency
/// issues with the singleton MongoDB/Redis components in Infrastructure).
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(ApplicationServiceCollectionExtensions).Assembly);

        services.AddValidatorsFromAssemblyContaining<AdmissionRecordValidator>(ServiceLifetime.Singleton);

        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IForecastService, ForecastService>();
        services.AddSingleton<IProbabilityService, ProbabilityService>();
        services.AddSingleton<ISpecialtyQueryService, SpecialtyQueryService>();
        services.AddSingleton<IExcelImportService, ExcelImportService>();

        // Grant-side use-cases run in parallel to the threshold ones and share
        // the same DI lifetimes; the two streams never cross over.
        services.AddSingleton<IGrantForecastService, GrantForecastService>();
        services.AddSingleton<IGrantQueryService, GrantQueryService>();
        services.AddSingleton<IGrantImportService, GrantImportService>();

        return services;
    }
}
