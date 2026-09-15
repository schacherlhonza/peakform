using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Integrations;
using TrainCoach.Integrations.Mock;
using TrainCoach.Integrations.Strava;

namespace TrainCoach.Integrations;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StravaOptions>(configuration.GetSection(StravaOptions.SectionName));
        services.AddHttpClient(nameof(StravaIntegrationProvider));
        services.AddScoped<IIntegrationProvider, StravaIntegrationProvider>();

        services.AddScoped<IIntegrationProvider, GarminDemoProvider>();

        services.AddScoped<MySasyDemoProvider>();
        services.AddScoped<IIntegrationProvider>(sp => sp.GetRequiredService<MySasyDemoProvider>());

        return services;
    }
}
