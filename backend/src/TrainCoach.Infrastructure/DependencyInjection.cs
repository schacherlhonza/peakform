using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainCoach.Application.Auth;
using TrainCoach.Application.Common;
using TrainCoach.Infrastructure.BackgroundJobs;
using TrainCoach.Infrastructure.Common;
using TrainCoach.Infrastructure.Identity;
using TrainCoach.Infrastructure.Persistence;
using TrainCoach.Infrastructure.Security;

namespace TrainCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Chybí connection string \"Default\".");

        services.AddDbContext<TrainCoachDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TrainCoachDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<TrainCoachDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection().SetApplicationName("TrainCoach");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<ITokenEncryptor, DataProtectionTokenEncryptor>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddSingleton<ChannelBackgroundJobQueue>();
        services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<ChannelBackgroundJobQueue>());
        services.AddHostedService<QueuedHostedService>();

        return services;
    }
}
