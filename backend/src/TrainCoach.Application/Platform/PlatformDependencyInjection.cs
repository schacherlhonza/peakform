using Microsoft.Extensions.DependencyInjection;

namespace TrainCoach.Application.Platform;

public static class PlatformDependencyInjection
{
    public static IServiceCollection AddPlatform(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
