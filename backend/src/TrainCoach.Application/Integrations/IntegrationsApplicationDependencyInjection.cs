using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Application.Integrations.StravaArchive;

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
        services.AddScoped<IActivityIngestionService, ActivityIngestionService>();
        services.AddScoped<IActivityStreamBackfillJob, ActivityStreamBackfillJob>();
        services.AddScoped<IStravaApiDataPurgeService, StravaApiDataPurgeService>();
        services.AddScoped<IConnectorPolicyService, ConnectorPolicyService>();
        services.AddScoped<IBackfillActivitySourceRecordsCommand, BackfillActivitySourceRecordsCommand>();
        services.AddScoped<IDuplicateDryRunReportService, DuplicateDryRunReportService>();

        services.Configure<StravaArchiveImportOptions>(configuration.GetSection(StravaArchiveImportOptions.SectionName));
        services.AddScoped<IStravaArchiveImportService, StravaArchiveImportService>();
        services.AddScoped<IStravaArchiveImportJob, StravaArchiveImportJob>();

        return services;
    }
}
