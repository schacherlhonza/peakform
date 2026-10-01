using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;
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
}
