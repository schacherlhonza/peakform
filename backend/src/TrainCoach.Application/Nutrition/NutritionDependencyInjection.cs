using Microsoft.Extensions.DependencyInjection;

namespace TrainCoach.Application.Nutrition;

public static class NutritionDependencyInjection
{
    public static IServiceCollection AddNutrition(this IServiceCollection services)
    {
        services.AddScoped<IFoodEntryService, FoodEntryService>();
        services.AddScoped<IHydrationEntryService, HydrationEntryService>();

        return services;
    }
}
