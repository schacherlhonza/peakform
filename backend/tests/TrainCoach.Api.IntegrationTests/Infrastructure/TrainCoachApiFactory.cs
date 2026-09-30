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
/// SQLite database instead of PostgreSQL — Docker/Testcontainers aren't available in
/// this sandbox (see docs/architecture.md). A fresh factory+database is created per test class
/// instance (xUnit makes one instance per test method), so tests never see each other's data.
/// CI additionally runs the full suite against a real Postgres service container.
/// The database is a temp file rather than a single shared in-memory connection: the background
/// worker runs jobs concurrently with the test's own polling requests, and one SqliteConnection
/// used from two threads at once corrupts its state ("another row available", concurrent
/// collection update). With a file, every DbContext gets its own connection and SQLite's
/// locking (plus the default busy timeout) serializes writers.
/// </summary>
public class TrainCoachApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"traincoach-tests-{Guid.NewGuid():N}.db");
    private readonly string _archiveStoragePath = Path.Combine(Path.GetTempPath(), $"traincoach-tests-archives-{Guid.NewGuid():N}");
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("StravaArchiveImport:StoragePath", _archiveStoragePath);

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

            services.AddDbContext<TrainCoachDbContext>(options => options.UseSqlite(ConnectionString));
        });
    }

    /// <summary>Program.cs skips schema/role setup entirely for the "Testing" environment (see
    /// its comment), so the test host owns that here instead — call once per factory before use.</summary>
    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await DbInitializer.EnsureRolesAsync(roleManager);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Pooled connections keep the file open; release this database's pool before deleting.
            using (var connection = new SqliteConnection(ConnectionString))
            {
                SqliteConnection.ClearPool(connection);
            }
            try
            {
                Directory.Delete(_archiveStoragePath, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or DirectoryNotFoundException)
            {
            }
            foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // A straggling background job still has it open — it's in the temp dir anyway.
                }
            }
        }
    }
}
