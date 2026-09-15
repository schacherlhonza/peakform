using Microsoft.Extensions.DependencyInjection;

namespace TrainCoach.Application.Integrations;

public static class IntegrationsApplicationDependencyInjection
{
    public static IServiceCollection AddIntegrationsApplication(this IServiceCollection services)
    {
        services.AddScoped<IIntegrationConnectionService, IntegrationConnectionService>();
        services.AddScoped<ISyncOrchestrator, SyncOrchestrator>();
        services.AddScoped<IImportService, ImportService>();

        return services;
    }
}
