using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Application.Wellness;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Integrations;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// End-to-end coverage of the sync pipeline's dedup/matching/policy wiring against a real EF Core
/// (SQLite) database — the scenario this whole rework exists for: the same real-world activity
/// arriving from two different connected sources must never become two canonical activities.
/// </summary>
public class SyncOrchestratorMatchingTests : IAsyncLifetime
{
    private FakeProviderApiFactory _factory = null!;
    private HttpClient _client = null!;
    private static readonly DateTime StartedAt = new(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        _factory = new FakeProviderApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> RegisterAthleteAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@test.cz",
            password = "Heslo123",
            firstName = "Test",
            lastName = "Athlete",
            role = "Athlete",
            timeZoneId = "Europe/Prague",
            locale = "cs-CZ",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResultDto>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        return Guid.Parse(body!.UserId);
    }

    private record AuthResultDto(string UserId, string Email);

    private static async Task<Guid> ConnectAsync(IApplicationDbContext db, ITokenEncryptor tokenEncryptor, Guid athleteUserId, IntegrationProviderType provider)
    {
        var connection = new IntegrationConnection
        {
            AthleteUserId = athleteUserId,
            Provider = provider,
            Status = IntegrationConnectionStatus.Connected,
            ConnectedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Credential = new IntegrationCredential { EncryptedAccessToken = tokenEncryptor.Protect("fake-access-token") },
        };
        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync();
        return connection.Id;
    }

    [Fact]
    public async Task GarminActivityViaStravaAndIntervalsIcu_AutoMergesIntoOneCanonicalActivity()
    {
        var athleteUserId = await RegisterAthleteAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var tokenEncryptor = scope.ServiceProvider.GetRequiredService<ITokenEncryptor>();
        var policyService = scope.ServiceProvider.GetRequiredService<IConnectorPolicyService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();

        var intervalsConnectionId = await ConnectAsync(db, tokenEncryptor, athleteUserId, IntegrationProviderType.IntervalsIcu);
        var stravaConnectionId = await ConnectAsync(db, tokenEncryptor, athleteUserId, IntegrationProviderType.Strava);
        await policyService.EnsureDefaultsAsync(athleteUserId);

        // Strava's own adapter never surfaces this today, but a device-attributed hint (via
        // intervals.icu) is exactly the kind of signal a future direct source could also report —
        // exercising it here alongside the required exact-fingerprint case.
        _factory.IntervalsIcuActivities.Add(new ExternalActivity(
            "ivl-1", SportType.Running, "Morning Run", StartedAt, 3600, 10000,
            null, null, null, null, null, null, DeviceName: "Garmin Forerunner 965"));
        _factory.StravaActivities.Add(new ExternalActivity(
            "strava-1", SportType.Running, "Ranní běh", StartedAt.AddSeconds(5), 3605, 10010,
            null, null, null, null, null, null, DeviceName: "Garmin Forerunner 965"));

        await orchestrator.RunAsync(intervalsConnectionId, SyncTrigger.Manual);
        await orchestrator.RunAsync(stravaConnectionId, SyncTrigger.Manual);

        var runErrors = await db.SynchronizationRuns.Where(r => r.ErrorMessage != null).Select(r => r.ErrorMessage).ToListAsync();
        runErrors.Should().BeEmpty("sync runs should succeed: " + string.Join(" | ", runErrors));

        var activities = await db.CompletedActivities
            .Include(a => a.SourceRecords)
            .Where(a => a.AthleteUserId == athleteUserId)
            .ToListAsync();

        activities.Should().HaveCount(1, "the Strava sync must attach to the existing intervals.icu activity, not create a second one");
        activities[0].SourceRecords.Should().HaveCount(2);
        activities[0].MatchStatus.Should().Be(ActivityMatchStatus.AutoMerged);
        activities[0].SourceRecords.Select(sr => sr.Source).Should().BeEquivalentTo([DataSource.IntervalsIcu, DataSource.Strava]);

        var mergeDecisions = await db.MergeDecisions.Where(m => m.AthleteUserId == athleteUserId).ToListAsync();
        mergeDecisions.Should().ContainSingle();
        mergeDecisions[0].Outcome.Should().Be(MergeDecisionOutcome.Merged);

        // Once intervals.icu is connected, Strava's default Activities policy must have flipped
        // to FallbackOnly automatically — the concrete mechanism behind the merge above.
        var stravaMode = await policyService.GetEffectiveModeAsync(athleteUserId, IntegrationProviderType.Strava, DataDomain.Activities);
        stravaMode.Should().Be(ConnectorMode.FallbackOnly);
    }

    [Fact]
    public async Task TwoDifferentRunsSameDay_DoNotMerge()
    {
        var athleteUserId = await RegisterAthleteAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var tokenEncryptor = scope.ServiceProvider.GetRequiredService<ITokenEncryptor>();
        var policyService = scope.ServiceProvider.GetRequiredService<IConnectorPolicyService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();

        var intervalsConnectionId = await ConnectAsync(db, tokenEncryptor, athleteUserId, IntegrationProviderType.IntervalsIcu);
        await policyService.EnsureDefaultsAsync(athleteUserId);

        // A short morning shakeout and a real evening tempo run — same day, same sport, genuinely
        // different activities.
        _factory.IntervalsIcuActivities.Add(new ExternalActivity("ivl-morning", SportType.Running, "Shakeout", StartedAt, 1200, 2000, null, null, null, null, null, null));
        await orchestrator.RunAsync(intervalsConnectionId, SyncTrigger.Manual);

        _factory.IntervalsIcuActivities.Add(new ExternalActivity("ivl-evening", SportType.Running, "Tempo", StartedAt.AddHours(10), 2700, 8000, null, null, null, null, null, null));
        await orchestrator.RunAsync(intervalsConnectionId, SyncTrigger.Manual);

        var activities = await db.CompletedActivities.Where(a => a.AthleteUserId == athleteUserId).ToListAsync();
        activities.Should().HaveCount(2);
        activities.Should().OnlyContain(a => a.MatchStatus == ActivityMatchStatus.Unambiguous);
    }

    [Fact]
    public async Task WellnessFromTwoSources_NeverAveraged_SelectionPicksHighestPrecedence()
    {
        var athleteUserId = await RegisterAthleteAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var tokenEncryptor = scope.ServiceProvider.GetRequiredService<ITokenEncryptor>();
        var policyService = scope.ServiceProvider.GetRequiredService<IConnectorPolicyService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestrator>();
        var selectionService = scope.ServiceProvider.GetRequiredService<IDailyMetricSelectionService>();

        var intervalsConnectionId = await ConnectAsync(db, tokenEncryptor, athleteUserId, IntegrationProviderType.IntervalsIcu);
        await policyService.EnsureDefaultsAsync(athleteUserId);

        var date = DateOnly.FromDateTime(StartedAt);
        _factory.IntervalsIcuWellness.Add(new ExternalWellnessSample(date, HrvRmssdMs: 55m, RestingHeartRateBpm: 48, ReadinessScore: 80));
        await orchestrator.RunAsync(intervalsConnectionId, SyncTrigger.Manual);

        // A second, independent source for the same day/metric — must be stored as its own row,
        // never averaged into or overwriting the intervals.icu one.
        db.HrvMeasurements.Add(new TrainCoach.Domain.Wellness.HrvMeasurement
        {
            AthleteUserId = athleteUserId, Date = date, RmssdMs = 40m, Source = DataSource.Manual, CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await selectionService.RecomputeForAthleteDayAsync(athleteUserId, date);

        var hrvRows = await db.HrvMeasurements.Where(h => h.AthleteUserId == athleteUserId && h.Date == date).ToListAsync();
        hrvRows.Should().HaveCount(2, "both source observations must be kept, never averaged or overwritten");

        var selection = (await selectionService.GetForAthleteDateRangeAsync(athleteUserId, date, date))
            .Single(s => s.MetricKind == WellnessMetricKind.Hrv);
        selection.SelectedSource.Should().Be(DataSource.IntervalsIcu, "intervals.icu outranks Manual in the default precedence table");
        selection.SelectedValue.Should().Be(55m);
    }
}
