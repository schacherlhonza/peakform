using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class HeartRateZonesApiTests : IntegrationTestBase
{
    private record ZoneDto(int ZoneNumber, string Name, int MinBpm, int MaxBpm);

    // The values from the athlete's settings page, zone 1 starting at 0.
    private static object Request(string athleteUserId) => new
    {
        athleteUserId,
        effectiveFromDate = "2025-01-01",
        zones = new[]
        {
            new { zoneNumber = 1, name = "Z1 Regenerace", minBpm = 0, maxBpm = 144 },
            new { zoneNumber = 2, name = "Z2 Vytrvalost", minBpm = 145, maxBpm = 155 },
            new { zoneNumber = 3, name = "Z3 Tempo", minBpm = 156, maxBpm = 161 },
            new { zoneNumber = 4, name = "Z4 Práh", minBpm = 162, maxBpm = 167 },
            new { zoneNumber = 5, name = "Z5 VO2max", minBpm = 168, maxBpm = 190 },
        },
    };

    [Fact]
    public async Task Athlete_can_set_own_zones()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);

        var response = await client.PutAsJsonAsync($"/api/athletes/{auth.UserId}/heart-rate-zones", Request(auth.UserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var zones = (await client.GetFromJsonAsync<List<ZoneDto>>($"/api/athletes/{auth.UserId}/heart-rate-zones", JsonOptions))!;
        zones.Should().HaveCount(5);
        zones.Single(z => z.ZoneNumber == 1).MinBpm.Should().Be(0);
    }

    [Fact]
    public async Task Another_athlete_cannot_set_my_zones()
    {
        var owner = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var other = AuthenticatedClient(await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"));

        var response = await other.PutAsJsonAsync($"/api/athletes/{owner.UserId}/heart-rate-zones", Request(owner.UserId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Saving_zones_computes_time_in_zones_for_activities_with_a_stored_stream()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);

        // 2017 activity — before the zones' 2025 start, so the earliest set applies.
        // 10 min at 150 bpm (Z2), then 5 min at 165 (Z4), 1 Hz.
        Guid activityId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var start = new DateTime(2017, 5, 1, 6, 0, 0, DateTimeKind.Utc);
            var builder = new ActivityStreamBuilder();
            for (var t = 0; t <= 900; t++)
            {
                builder.Add(new ActivityStreamBuilder.Sample(start.AddSeconds(t), HeartRate: t < 600 ? 150 : 165));
            }
            var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{auth.UserId}-z", FetchedAtUtc = DateTime.UtcNow };
            var activity = new CompletedActivity
            {
                AthleteUserId = Guid.Parse(auth.UserId), Sport = SportType.Running, StartedAtUtc = start, DurationSeconds = 900,
                CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
            };
            db.CompletedActivities.Add(activity);
            db.ActivityStreams.Add(ActivityStreamMapping.ToEntity(record.Id, builder.Build()!, ActivityStreamOrigin.StravaArchive, DateTime.UtcNow));
            await db.SaveChangesAsync();
            activityId = activity.Id;
        }

        (await client.PutAsJsonAsync($"/api/athletes/{auth.UserId}/heart-rate-zones", Request(auth.UserId))).EnsureSuccessStatusCode();

        Dictionary<string, decimal> zoneSeconds = [];
        for (var attempt = 0; attempt < 100 && zoneSeconds.Count == 0; attempt++)
        {
            await Task.Delay(100);
            var dto = await client.GetFromJsonAsync<JsonElement>($"/api/activities/{activityId}", JsonOptions);
            zoneSeconds = dto.GetProperty("additionalMetrics").ValueKind == JsonValueKind.Array
                ? dto.GetProperty("additionalMetrics").EnumerateArray()
                    .Where(m => m.GetProperty("type").GetString()!.StartsWith("TimeInHrZone"))
                    .ToDictionary(m => m.GetProperty("type").GetString()!, m => m.GetProperty("value").GetDecimal())
                : [];
        }

        zoneSeconds.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["TimeInHrZone2"] = 600, ["TimeInHrZone4"] = 300 });
    }
}
