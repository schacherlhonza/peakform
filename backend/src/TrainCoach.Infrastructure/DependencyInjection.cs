using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrainCoach.Application.Account;
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

        // Persisted to a fixed path (mounted as a durable volume in docker-compose.yml) so the key
        // ring survives container restarts/recreates — otherwise every restart silently generates
        // a fresh key and permanently orphans every credential (Strava tokens, etc.) encrypted
        // with the previous one, since the default ephemeral-filesystem key store isn't kept.
        var dataProtectionKeysPath = configuration["DataProtection:KeysPath"] ?? "dataprotection-keys";
        services.AddDataProtection()
            .SetApplicationName("TrainCoach")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services.AddScoped<ITokenEncryptor, DataProtectionTokenEncryptor>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddSingleton<ChannelBackgroundJobQueue>();
        services.AddSingleton<IBackgroundJobQueue>(sp => sp.GetRequiredService<ChannelBackgroundJobQueue>());
        services.AddHostedService<QueuedHostedService>();

        return services;
    }
}
