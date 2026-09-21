using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TrainCoach.Api.Auth;
using TrainCoach.Api.Middleware;
using TrainCoach.Application;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Infrastructure;
using TrainCoach.Infrastructure.Identity;
using TrainCoach.Integrations;
using TrainCoach.Infrastructure.Persistence;
using TrainCoach.Infrastructure.Security;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

    // --- Services -----------------------------------------------------

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddIntegrations(builder.Configuration);

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationActionFilter>();
    }).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
    var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("auth", limiterOptions =>
        {
            limiterOptions.PermitLimit = 10;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
        });
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "TrainCoach API", Version = "v1" });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Zadejte JWT access token (bez prefixu \"Bearer \").",
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                []
            },
        });
    });

    var app = builder.Build();

    // --- Startup: migrate + seed roles ---
    // Skipped for the "Testing" host (WebApplicationFactory + SQLite, see
    // TrainCoach.Api.IntegrationTests / TrainCoachApiFactory), which owns its own in-memory
    // schema + role seeding — this keeps `ASPNETCORE_ENVIRONMENT=Testing` usable standalone too
    // (e.g. to regenerate openapi.json without a real Postgres instance running).

    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await DbInitializer.EnsureRolesAsync(roleManager);
    }

    // Demo data (one coach, two athletes, a four-week training block, wellness/nutrition
    // history, comments...) only for local development — never for Production, and the
    // Testing host manages its own minimal data per-test. Idempotent: skipped if already seeded.
    if (app.Environment.IsDevelopment())
    {
        await DemoDataSeeder.SeedAsync(app.Services);
    }

    // --- Ops CLI: read-only intervals.icu diagnostic ---------------------
    // `--diagnose-intervals-icu <athleteUserId>` calls intervals.icu's raw activities endpoint
    // directly (bypassing IntervalsIcuIntegrationProvider's STRAVA-source filter) so we can see
    // exactly how many activities exist per "source" tag for that athlete. Read-only, no writes.
    if (args.Length >= 2 && args[0] == "--diagnose-intervals-icu")
    {
        var athleteUserId = Guid.Parse(args[1]);
        using var diagScope = app.Services.CreateScope();
        var db = diagScope.ServiceProvider.GetRequiredService<TrainCoach.Application.Common.IApplicationDbContext>();
        var tokenEncryptor = diagScope.ServiceProvider.GetRequiredService<TrainCoach.Application.Common.ITokenEncryptor>();
        var httpClientFactory = diagScope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        var connection = await db.IntegrationConnections.Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.AthleteUserId == athleteUserId && c.Provider == TrainCoach.Domain.Enums.IntegrationProviderType.IntervalsIcu);
        if (connection?.Credential is null)
        {
            Console.WriteLine("[Diag] No intervals.icu connection/credential found for this athlete.");
            return;
        }

        var accessToken = tokenEncryptor.Unprotect(connection.Credential.EncryptedAccessToken);
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var oldest = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35));
        var newest = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var response = await client.GetAsync($"https://intervals.icu/api/v1/athlete/0/activities?oldest={oldest:yyyy-MM-dd}&newest={newest:yyyy-MM-dd}");
        var body = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Diag] intervals.icu status={(int)response.StatusCode}");

        if (response.IsSuccessStatusCode)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var bySource = new Dictionary<string, int>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var source = el.TryGetProperty("source", out var s) ? (s.GetString() ?? "null") : "(missing)";
                bySource[source] = bySource.GetValueOrDefault(source) + 1;
                var id = el.TryGetProperty("id", out var idp) ? idp.GetString() : "?";
                var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                var startDate = el.TryGetProperty("start_date_local", out var sd) ? sd.GetString() : null;
                var deviceName = el.TryGetProperty("device_name", out var dn) ? dn.GetString() : null;
                Console.WriteLine($"[Diag]   id={id} source={source} device={deviceName} start={startDate} name={name}");
            }
            Console.WriteLine("[Diag] counts by source: " + string.Join(", ", bySource.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        else
        {
            Console.WriteLine("[Diag] body=" + body);
        }

        var wellnessResponse = await client.GetAsync($"https://intervals.icu/api/v1/athlete/0/wellness?oldest={oldest:yyyy-MM-dd}&newest={newest:yyyy-MM-dd}");
        var wellnessBody = await wellnessResponse.Content.ReadAsStringAsync();
        Console.WriteLine($"[Diag] intervals.icu wellness status={(int)wellnessResponse.StatusCode}");
        if (wellnessResponse.IsSuccessStatusCode)
        {
            using var wdoc = System.Text.Json.JsonDocument.Parse(wellnessBody);
            foreach (var el in wdoc.RootElement.EnumerateArray())
            {
                string? Get(string name) => el.TryGetProperty(name, out var v) && v.ValueKind != System.Text.Json.JsonValueKind.Null ? v.ToString() : null;
                Console.WriteLine($"[Diag-Wellness] date={Get("id")} hrv={Get("hrv")} restingHR={Get("restingHR")} sleepSecs={Get("sleepSecs")} sleepScore={Get("sleepScore")} readiness={Get("readiness")} weight={Get("weight")} spO2={Get("spO2")} steps={Get("steps")} vo2max={Get("vo2max")}");
            }
        }
        else
        {
            Console.WriteLine("[Diag-Wellness] body=" + wellnessBody);
        }

        return;
    }

    // `--resync-connection <integrationConnectionId>` runs a real, normal Manual sync through
    // ISyncOrchestrator for one connection (same code path a "Sync Now" click uses) and prints the
    // resulting run — useful for verifying a fix without needing the athlete's password. On
    // success this also naturally flips the connection back to Connected, same as any real sync.
    if (args.Length >= 2 && args[0] == "--resync-connection")
    {
        var connectionId = Guid.Parse(args[1]);
        using var resyncScope = app.Services.CreateScope();
        var orchestrator = resyncScope.ServiceProvider.GetRequiredService<TrainCoach.Application.Integrations.ISyncOrchestrator>();
        var db = resyncScope.ServiceProvider.GetRequiredService<TrainCoach.Application.Common.IApplicationDbContext>();

        await orchestrator.RunAsync(connectionId, TrainCoach.Domain.Enums.SyncTrigger.Manual);

        var run = await db.SynchronizationRuns.Where(r => r.IntegrationConnectionId == connectionId).OrderByDescending(r => r.StartedAtUtc).FirstAsync();
        var connection = await db.IntegrationConnections.FirstAsync(c => c.Id == connectionId);
        Console.WriteLine($"[Resync] connectionStatus={connection.Status} runStatus={run.Status} fetched={run.ItemsFetched} created={run.ItemsCreated} updated={run.ItemsUpdated} skippedDuplicate={run.ItemsSkippedDuplicate} flaggedForReview={run.ItemsFlaggedForReview} error={run.ErrorMessage}");
        return;
    }

    // --- Ops CLI: non-destructive migration tooling ---------------------
    // `--backfill-and-dry-run` runs the idempotent PrimarySourceRecordId/NormalizedFingerprint
    // backfill (see docs/integrations/canonical-data-and-deduplication-plan.md, migration step 2)
    // followed by a read-only duplicate-matching dry run (step 3) and exits — it never starts
    // Kestrel, and never writes MergeDecision/DuplicateCandidate rows itself. Safe to re-run.
    if (args.Contains("--backfill-and-dry-run"))
    {
        using var opsScope = app.Services.CreateScope();

        var backfillCommand = opsScope.ServiceProvider.GetRequiredService<IBackfillActivitySourceRecordsCommand>();
        var backfillResult = await backfillCommand.ExecuteAsync();
        Console.WriteLine($"[Backfill] scanned={backfillResult.ActivitiesScanned} backfilled={backfillResult.ActivitiesBackfilled} " +
                           $"alreadyDone={backfillResult.ActivitiesSkippedAlreadyBackfilled} skippedMultipleSources={backfillResult.ActivitiesSkippedMultipleSources}");

        var dryRunService = opsScope.ServiceProvider.GetRequiredService<IDuplicateDryRunReportService>();
        var report = await dryRunService.GenerateAsync(athleteUserId: null);
        Console.WriteLine($"[DryRun] scanned={report.TotalActivitiesScanned} exact={report.ExactTierCount} " +
                           $"highConfidence={report.HighConfidenceTierCount} uncertain={report.UncertainTierCount}");
        Console.WriteLine($"[DryRun] candidates={report.CandidatesJson}");

        return;
    }

    // --- Middleware pipeline -------------------------------------------

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseExceptionHandler();

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), camera=(), microphone=()");
        await next();
    });

    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseCors("Default");

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "TrainCoach.Api se nepodařilo spustit.");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposed so WebApplicationFactory&lt;Program&gt; can bootstrap the Api host for integration tests.</summary>
public partial class Program;
