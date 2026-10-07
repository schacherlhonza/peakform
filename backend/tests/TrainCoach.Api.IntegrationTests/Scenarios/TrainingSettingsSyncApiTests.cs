using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>Saving zones in PeakForm writes them to the athlete's connected intervals.icu (fake provider, no network).</summary>
public class TrainingSettingsSyncApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private FakeProviderApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _athleteUserId;

    private record AuthResultDto(string UserId, string AccessToken);
    private record ThresholdsDto(int? ThresholdPaceSecondsPerKm);

    public async Task InitializeAsync()
    {
        _factory = new FakeProviderApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@test.cz", password = "Heslo123", firstName = "Test", lastName = "Athlete", role = "Athlete",
        });
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        _athleteUserId = Guid.Parse(auth.UserId);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task ConnectIntervalsIcuAsync(string grantedScope)
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

    private Task<HttpResponseMessage> SaveZonesAsync(string effectiveFromDate = "2025-01-01") =>
        _client.PutAsJsonAsync($"/api/athletes/{_athleteUserId}/heart-rate-zones", new
        {
            athleteUserId = _athleteUserId,
            effectiveFromDate,
            zones = new[]
            {
                new { zoneNumber = 1, name = "Z1 Regenerace", minBpm = 130, maxBpm = 144 },
                new { zoneNumber = 2, name = "Z2 Vytrvalost", minBpm = 145, maxBpm = 155 },
                new { zoneNumber = 3, name = "Z3 Tempo", minBpm = 156, maxBpm = 161 },
                new { zoneNumber = 4, name = "Z4 Práh", minBpm = 162, maxBpm = 167 },
                new { zoneNumber = 5, name = "Z5 VO2max", minBpm = 168, maxBpm = 190 },
            },
        });

    /// <summary>The job runs on the background queue — wait until it recorded an outcome.</summary>
    private async Task<IntegrationConnection> WaitForSyncOutcomeAsync()
    {
        for (var i = 0; i < 100; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var connection = await db.IntegrationConnections.AsNoTracking()
                .SingleAsync(c => c.AthleteUserId == _athleteUserId && c.Provider == IntegrationProviderType.IntervalsIcu);
            if (connection.HeartRateZonesSyncedAtUtc is not null || connection.HeartRateZonesSyncError is not null)
            {
                return connection;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Zone sync never recorded an outcome.");
    }

    [Fact]
    public async Task Saving_zones_writes_their_upper_bounds_to_intervals_icu()
    {
        await ConnectIntervalsIcuAsync("ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE,SETTINGS:WRITE");

        (await SaveZonesAsync()).EnsureSuccessStatusCode();

        var connection = await WaitForSyncOutcomeAsync();
        connection.HeartRateZonesSyncError.Should().BeNull();
        connection.HeartRateZonesSyncedAtUtc.Should().NotBeNull();
        var pushed = _factory.IntervalsIcuProvider.PushedSettings.Should().ContainSingle().Subject;
        pushed.HeartRateZones.Select(z => z.MaxBpm).Should().Equal(144, 155, 161, 167, 190);
        pushed.HeartRateZones.Select(z => z.Name).Should().StartWith("Z1 Regenerace");
        pushed.ThresholdPaceSecondsPerKm.Should().BeNull("no threshold pace was set");
    }

    [Fact]
    public async Task Without_the_settings_scope_the_athlete_is_asked_to_reconnect()
    {
        await ConnectIntervalsIcuAsync("ACTIVITY:READ,WELLNESS:READ,CALENDAR:WRITE");

        (await SaveZonesAsync()).EnsureSuccessStatusCode();

        var connection = await WaitForSyncOutcomeAsync();
        connection.HeartRateZonesSyncError.Should().Contain("znovu připojit");
        connection.HeartRateZonesSyncedAtUtc.Should().BeNull();
        _factory.IntervalsIcuProvider.PushedSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rejection_is_recorded_and_the_zones_stay_saved()
    {
        await ConnectIntervalsIcuAsync("SETTINGS:WRITE");
        _factory.IntervalsIcuProvider.SettingsPushError = "intervals.icu zóny nepřijalo (HTTP 422).";

        var response = await SaveZonesAsync();

        response.EnsureSuccessStatusCode();
        (await WaitForSyncOutcomeAsync()).HeartRateZonesSyncError.Should().Be("intervals.icu zóny nepřijalo (HTTP 422).");
    }

    [Fact]
    public async Task Saving_the_threshold_pace_writes_it_together_with_the_current_zones()
    {
        await ConnectIntervalsIcuAsync("SETTINGS:WRITE");
        (await SaveZonesAsync()).EnsureSuccessStatusCode();
        await WaitForSyncOutcomeAsync();

        var response = await _client.PutAsJsonAsync($"/api/athletes/{_athleteUserId}/thresholds", new { thresholdPaceSecondsPerKm = 270 });

        response.EnsureSuccessStatusCode();
        (await _client.GetFromJsonAsync<ThresholdsDto>($"/api/athletes/{_athleteUserId}/thresholds", JsonOptions))!.ThresholdPaceSecondsPerKm.Should().Be(270);
        for (var i = 0; i < 100 && _factory.IntervalsIcuProvider.PushedSettings.Count < 2; i++) await Task.Delay(100);
        var pushed = _factory.IntervalsIcuProvider.PushedSettings[^1];
        pushed.ThresholdPaceSecondsPerKm.Should().Be(270);
        pushed.HeartRateZones.Should().HaveCount(5);
    }

    [Fact]
    public async Task An_implausible_threshold_pace_is_rejected()
    {
        var response = await _client.PutAsJsonAsync($"/api/athletes/{_athleteUserId}/thresholds", new { thresholdPaceSecondsPerKm = 45 });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Zones_starting_in_the_future_are_not_written_yet()
    {
        await ConnectIntervalsIcuAsync("SETTINGS:WRITE");

        (await SaveZonesAsync(DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"))).EnsureSuccessStatusCode();
        await Task.Delay(1000);

        _factory.IntervalsIcuProvider.PushedSettings.Should().BeEmpty();
    }
}
