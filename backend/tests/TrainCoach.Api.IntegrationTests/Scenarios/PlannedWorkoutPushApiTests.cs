using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// Coach-planned workouts reach the athlete's intervals.icu calendar (and so Garmin) only with the
/// athlete's consent, and follow every change (fake provider, no network).
/// </summary>
public class PlannedWorkoutPushApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private FakeProviderApiFactory _factory = null!;
    private HttpClient _coach = null!;
    private HttpClient _athlete = null!;
    private Guid _athleteUserId;
    private Guid _weekId;

    private record AuthResultDto(string UserId, string AccessToken);
    private record IdDto(Guid Id);
    private record PushStatusDto(IntegrationProviderType Provider, WorkoutPushStatus Status, string? Error, List<string> Warnings);

    public async Task InitializeAsync()
    {
        _factory = new FakeProviderApiFactory();
        await _factory.InitializeDatabaseAsync();

        var athleteEmail = $"athlete-{Guid.NewGuid():N}@example.com";
        (_coach, _) = await RegisterAsync($"coach-{Guid.NewGuid():N}@example.com", "Coach");
        (_athlete, _athleteUserId) = await RegisterAsync(athleteEmail, "Athlete");

        var invite = await _coach.PostAsJsonAsync("/api/relationships/invite", new { athleteEmail, note = "" });
        var relationship = (await invite.Content.ReadFromJsonAsync<IdDto>(JsonOptions))!;
        (await _athlete.PostAsJsonAsync($"/api/relationships/{relationship.Id}/respond", new { accept = true })).EnsureSuccessStatusCode();

        var monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        var plan = await _coach.PostAsJsonAsync("/api/plans", new { athleteUserId = _athleteUserId, name = "Plán", startDate = monday.AddDays(-14) });
        var planId = (await plan.Content.ReadFromJsonAsync<IdDto>(JsonOptions))!.Id;
        var week = await _coach.PostAsJsonAsync($"/api/plans/{planId}/weeks", new { weekStartDate = monday, weekIndex = 1 });
        _weekId = (await week.Content.ReadFromJsonAsync<IdDto>(JsonOptions))!.Id;
    }

    public Task DisposeAsync()
    {
        _coach.Dispose();
        _athlete.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email, string role)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "Heslo123", firstName = "Test", lastName = "User", role });
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, Guid.Parse(auth.UserId));
    }

    private async Task ConnectIntervalsIcuAsync(string grantedScope = "ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE,SETTINGS:WRITE")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var tokenEncryptor = scope.ServiceProvider.GetRequiredService<ITokenEncryptor>();
        db.IntegrationConnections.Add(new IntegrationConnection
        {
            AthleteUserId = _athleteUserId,
            Provider = IntegrationProviderType.IntervalsIcu,
            Status = IntegrationConnectionStatus.Connected,
            ConnectedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Credential = new IntegrationCredential { EncryptedAccessToken = tokenEncryptor.Protect("fake-access-token"), GrantedScope = grantedScope },
        });
        await db.SaveChangesAsync();
    }

    private Task SetConsentAsync(bool enabled) =>
        _athlete.PutAsJsonAsync("/api/integrations/IntervalsIcu/push-planned-workouts", new { enabled }).ContinueWith(t => t.Result.EnsureSuccessStatusCode());

    private static object WorkoutBody(Guid weekId, DateOnly date, string title, bool isRestDay = false) => new
    {
        trainingWeekId = weekId, date, sport = isRestDay ? "Rest" : "Running", title, coachDescription = "", isRestDay,
        segments = isRestDay ? Array.Empty<object>() : new object[]
        {
            new { order = 1, type = "WarmUp", durationSeconds = 900, intensityTargetType = "HeartRateZone", targetHeartRateZoneNumber = 2 },
            new { order = 2, type = "Main", durationSeconds = 1200, intensityTargetType = "Rpe", targetRpe = 6 },
        },
    };

    private async Task<Guid> CreateWorkoutAsync(DateOnly date, string title, bool isRestDay = false)
    {
        var response = await _coach.PostAsJsonAsync("/api/workouts", WorkoutBody(_weekId, date, title, isRestDay));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdDto>(JsonOptions))!.Id;
    }

    private async Task<List<PushStatusDto>> PushStatusAsync(Guid workoutId) =>
        (await _coach.GetFromJsonAsync<List<PushStatusDto>>($"/api/workouts/{workoutId}/push-status", JsonOptions))!;

    /// <summary>Pushing runs on the background queue — poll the coach's status view until it settles.</summary>
    private async Task<PushStatusDto> WaitForStatusAsync(Guid workoutId, WorkoutPushStatus expected)
    {
        for (var i = 0; i < 100; i++)
        {
            if ((await PushStatusAsync(workoutId)).FirstOrDefault() is { } status && status.Status == expected) return status;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Workout {workoutId} never reached {expected}; last: {JsonSerializer.Serialize(await PushStatusAsync(workoutId), JsonOptions)}");
    }

    [Fact]
    public async Task Nothing_is_pushed_without_the_athletes_consent()
    {
        await ConnectIntervalsIcuAsync();

        var workoutId = await CreateWorkoutAsync(Today.AddDays(1), "Lehký běh");
        await Task.Delay(1000);

        _factory.IntervalsIcuProvider.PushedWorkouts.Should().BeEmpty();
        (await PushStatusAsync(workoutId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Consent_pushes_upcoming_workouts_and_every_change_follows()
    {
        await ConnectIntervalsIcuAsync();
        var upcoming = await CreateWorkoutAsync(Today.AddDays(1), "Lehký běh");
        await CreateWorkoutAsync(Today.AddDays(2), "Volno", isRestDay: true);
        var past = await CreateWorkoutAsync(Today.AddDays(-5), "Starý běh");

        await SetConsentAsync(true);

        var status = await WaitForStatusAsync(upcoming, WorkoutPushStatus.Pushed);
        status.Error.Should().BeNull();
        status.Warnings.Should().Equal("RpeSentAsText");
        // The create-time job may also run after consent — a repeated upsert of the same entry, harmless.
        _factory.IntervalsIcuProvider.PushedWorkouts.Select(p => p.WorkoutId).Distinct().Should().Equal([upcoming], "rest days and past workouts stay off the watch");
        _factory.IntervalsIcuProvider.PushedWorkouts[0].Description.Should().Contain("- 15m Z2 HR intensity=warmup");
        (await PushStatusAsync(past)).Should().BeEmpty();

        // An edit re-pushes the same calendar entry (same external id) …
        (await _coach.PutAsJsonAsync($"/api/workouts/{upcoming}", WorkoutBody(_weekId, Today.AddDays(3), "Delší běh"))).EnsureSuccessStatusCode();
        for (var i = 0; i < 100 && _factory.IntervalsIcuProvider.PushedWorkouts[^1].Title != "Delší běh"; i++) await Task.Delay(100);
        _factory.IntervalsIcuProvider.PushedWorkouts[^1].Should().Match<(Guid WorkoutId, string Title, DateOnly Date, string Description)>(
            p => p.WorkoutId == upcoming && p.Date == Today.AddDays(3));

        // … and deleting the workout removes it.
        // (the deleted workout itself is gone for the coach, so this is checked on the provider side).
        (await _coach.DeleteAsync($"/api/workouts/{upcoming}")).EnsureSuccessStatusCode();
        for (var i = 0; i < 100 && _factory.IntervalsIcuProvider.RemovedWorkouts.Count == 0; i++) await Task.Delay(100);
        _factory.IntervalsIcuProvider.RemovedWorkouts.Should().Equal(upcoming);
    }

    [Fact]
    public async Task Withdrawing_consent_removes_the_pushed_workouts()
    {
        await ConnectIntervalsIcuAsync();
        var workoutId = await CreateWorkoutAsync(Today.AddDays(1), "Lehký běh");
        await SetConsentAsync(true);
        await WaitForStatusAsync(workoutId, WorkoutPushStatus.Pushed);

        await SetConsentAsync(false);

        await WaitForStatusAsync(workoutId, WorkoutPushStatus.Removed);
        _factory.IntervalsIcuProvider.RemovedWorkouts.Should().Equal(workoutId);
    }

    [Fact]
    public async Task Without_the_calendar_scope_the_coach_sees_why()
    {
        await ConnectIntervalsIcuAsync("ACTIVITY:READ,WELLNESS:READ");
        var workoutId = await CreateWorkoutAsync(Today.AddDays(1), "Lehký běh");

        await SetConsentAsync(true);

        (await WaitForStatusAsync(workoutId, WorkoutPushStatus.Failed)).Error.Should().Contain("znovu připojit");
        _factory.IntervalsIcuProvider.PushedWorkouts.Should().BeEmpty();
    }
}
