using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Integrations;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Integrations.IntervalsIcu;
using TrainCoach.Integrations.Mock;
using TrainCoach.Integrations.Oura;
using TrainCoach.Integrations.Strava;
using TrainCoach.Integrations.StravaArchive;
using TrainCoach.Integrations.Whoop;

namespace TrainCoach.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StravaOptions>(configuration.GetSection(StravaOptions.SectionName));
        services.AddHttpClient(nameof(StravaIntegrationProvider));
        services.AddScoped<IIntegrationProvider, StravaIntegrationProvider>();

        // Strava archive import: redirects are followed by hand so each hop can be checked
        // against the allow-list, and the job's own (configurable) timeout replaces HttpClient's
        // 100 s default, which a multi-GB download would blow through.
        services.AddHttpClient(StravaArchiveDownloader.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IStravaArchiveDownloader, StravaArchiveDownloader>();
        services.AddSingleton<IActivityFileProbe, ActivityFileProbe>();

        services.Configure<IntervalsIcuOptions>(configuration.GetSection(IntervalsIcuOptions.SectionName));
        services.AddHttpClient(nameof(IntervalsIcuIntegrationProvider));
        services.AddScoped<IntervalsIcuIntegrationProvider>();
        services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<IntervalsIcuIntegrationProvider>());

        services.AddScoped<IIntegrationProvider, GarminDemoProvider>();

        services.AddScoped<MySasyDemoProvider>();
        services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<MySasyDemoProvider>());

        // Architecture-ready only — no real credentials exist; see OuraIntegrationProvider's doc
        // comment and docs/integrations/oura-whoop-activation.md.
        services.Configure<OuraOptions>(configuration.GetSection(OuraOptions.SectionName));
        services.AddScoped<OuraIntegrationProvider>();
        services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<OuraIntegrationProvider>());

        services.Configure<WhoopOptions>(configuration.GetSection(WhoopOptions.SectionName));
        services.AddScoped<WhoopIntegrationProvider>();
        services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<WhoopIntegrationProvider>());

        return services;
    }
}
