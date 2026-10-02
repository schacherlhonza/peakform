using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>The coach's view of an athlete's activity history and records.</summary>
public class CoachActivityAccessTests : IntegrationTestBase
{
    private record RelationshipDto(Guid Id, string Status);

    [Fact]
    public async Task Coach_sees_athletes_history_and_records_only_while_the_relationship_is_active()
    {
        var coach = AuthenticatedClient(await RegisterAsync($"coach-{Guid.NewGuid():N}@example.com", "Coach"));
        var athleteEmail = $"athlete-{Guid.NewGuid():N}@example.com";
        var athleteAuth = await RegisterAsync(athleteEmail, "Athlete");
        var athlete = AuthenticatedClient(athleteAuth);
        var history = $"/api/athletes/{athleteAuth.UserId}/activities/search";
        var records = $"/api/athletes/{athleteAuth.UserId}/personal-bests";

        (await coach.GetAsync(history)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var invite = await coach.PostAsJsonAsync("/api/relationships/invite", new { athleteEmail, note = "" });
        var relationship = (await invite.Content.ReadFromJsonAsync<RelationshipDto>(JsonOptions))!;
        (await athlete.PostAsJsonAsync($"/api/relationships/{relationship.Id}/respond", new { accept = true })).EnsureSuccessStatusCode();

        (await coach.GetAsync(history)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await coach.GetAsync(records)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await athlete.PostAsJsonAsync($"/api/relationships/{relationship.Id}/revoke", new { reason = "konec" })).EnsureSuccessStatusCode();

        (await coach.GetAsync(history)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await coach.GetAsync(records)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
