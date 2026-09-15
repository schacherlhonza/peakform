using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Infrastructure.Persistence;

namespace TrainCoach.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real Api host (real DI graph, real controllers, real auth) against a private
/// in-memory SQLite database instead of PostgreSQL — Docker/Testcontainers aren't available in
/// this sandbox (see docs/architecture.md). A fresh factory+connection is created per test class
/// instance (xUnit makes one instance per test method), so tests never see each other's data.
/// CI additionally runs the full suite against a real Postgres service container.
/// </summary>
public class TrainCoachApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TrainCoachApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddDbContext (called once for Npgsql in Program.cs via AddInfrastructure) doesn't
            // just register DbContextOptions<T> — since EF Core 5, repeated AddDbContext calls
            // for the same context type also accumulate IDbContextOptionsConfiguration<T>
            // entries rather than fully replacing them, so removing only DbContextOptions<T>
            // leaves the Npgsql configuration layered underneath the Sqlite one added below
            // ("two providers registered" error). Remove every service keyed by TrainCoachDbContext.
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(TrainCoachDbContext)
                            || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(TrainCoachDbContext))))
                .ToList();
            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TrainCoachDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>Program.cs skips schema/role setup entirely for the "Testing" environment (see
    /// its comment), so the test host owns that here instead — call once per factory before use.</summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await DbInitializer.EnsureRolesAsync(roleManager);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
