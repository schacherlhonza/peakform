using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces the real Strava/intervals.icu adapters with FakeIntegrationProvider so sync pipeline
/// tests (dedup, connector policy, matching) can control exactly what each source reports without
/// any network call or real OAuth exchange. Every other real service (SyncOrchestrator, the
/// matcher, connector policy, EF) runs unmodified against the same in-memory SQLite database as
/// the base factory.
/// </summary>
public class FakeProviderApiFactory : TrainCoachApiFactory
{
    public List<ExternalActivity> StravaActivities { get; } = [];
    public List<ExternalActivity> IntervalsIcuActivities { get; } = [];
    public List<ExternalWellnessSample> IntervalsIcuWellness { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var providerDescriptors = services.Where(d => d.ServiceType == typeof(IIntegrationProvider)).ToList();
            foreach (var descriptor in providerDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IIntegrationProvider>(new FakeIntegrationProvider(IntegrationProviderType.Strava, StravaActivities));
            services.AddSingleton<IIntegrationProvider>(new FakeIntegrationProvider(IntegrationProviderType.IntervalsIcu, IntervalsIcuActivities, IntervalsIcuWellness));
        });
    }
}
