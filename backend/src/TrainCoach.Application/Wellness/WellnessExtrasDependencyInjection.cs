using Microsoft.Extensions.DependencyInjection;

namespace TrainCoach.Application.Wellness;

/// <summary>
/// Registers the additional Wellness services (health flags, sleep, recovery, HRV, personal
/// records) that live alongside CheckInService but are wired up separately to avoid touching
/// DependencyInjection.cs while other modules are being added concurrently.
/// </summary>
public static class WellnessExtrasDependencyInjection
{
    public static IServiceCollection AddWellnessExtras(this IServiceCollection services)
    {
        services.AddScoped<IPainOrHealthFlagService, PainOrHealthFlagService>();
        services.AddScoped<ISleepRecordService, SleepRecordService>();
        services.AddScoped<IRecoveryMetricService, RecoveryMetricService>();
        services.AddScoped<IHrvMeasurementService, HrvMeasurementService>();
        services.AddScoped<IPersonalRecordService, PersonalRecordService>();

        return services;
    }
}
