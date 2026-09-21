using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Integrations.Matching;

namespace TrainCoach.Application.Integrations;

public static class IntegrationsApplicationDependencyInjection
{
    public static IServiceCollection AddIntegrationsApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIntegrationConnectionService, IntegrationConnectionService>();
        services.AddScoped<ISyncOrchestrator, SyncOrchestrator>();
        services.AddScoped<IAccessTokenResolver, AccessTokenResolver>();
        services.AddScoped<IImportService, ImportService>();

        services.Configure<ActivityMatchingOptions>(configuration.GetSection(ActivityMatchingOptions.SectionName));
        services.AddScoped<IActivityMatchingService, ActivityMatchingService>();
        services.AddScoped<IConnectorPolicyService, ConnectorPolicyService>();
        services.AddScoped<IBackfillActivitySourceRecordsCommand, BackfillActivitySourceRecordsCommand>();
        services.AddScoped<IDuplicateDryRunReportService, DuplicateDryRunReportService>();

        return services;
    }
}
