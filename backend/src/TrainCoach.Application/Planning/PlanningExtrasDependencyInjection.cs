using Microsoft.Extensions.DependencyInjection;

namespace TrainCoach.Application.Planning;

/// <summary>
/// Registers the Planning-area CRUD services added alongside the original Application layer
/// (Season/Goal/Race, WorkoutTemplate, HeartRateZone, CustomAbbreviation). Kept separate from
/// <see cref="TrainCoach.Application.DependencyInjection.AddApplication"/> so both files can be
/// edited concurrently; call <c>services.AddApplication().AddPlanningExtras();</c> during startup.
/// Validators are still auto-discovered by <c>AddValidatorsFromAssembly</c> in AddApplication.
/// </summary>
public static class PlanningExtrasDependencyInjection
{
    public static IServiceCollection AddPlanningExtras(this IServiceCollection services)
    {
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IRaceService, RaceService>();
        services.AddScoped<IWorkoutTemplateService, WorkoutTemplateService>();
        services.AddScoped<IHeartRateZoneService, HeartRateZoneService>();
        services.AddScoped<ICustomAbbreviationService, CustomAbbreviationService>();

        return services;
    }
}
