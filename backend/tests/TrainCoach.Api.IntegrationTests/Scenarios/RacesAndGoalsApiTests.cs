using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>An athlete manages their own races and goals: create, edit, delete.</summary>
public class RacesAndGoalsApiTests : IntegrationTestBase
{
    private record RaceDto(Guid Id, string Name, int? TargetTimeSeconds, int? ActualTimeSeconds);
    private record GoalDto(Guid Id, string Title, bool IsAchieved);
    private record IdDto(Guid Id);
    private record CommentDto(Guid? RaceId, Guid? PlannedWorkoutId, string AuthorRole, string Text);

    [Fact]
    public async Task Athlete_edits_and_deletes_own_race_and_the_result_survives_an_edit()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        var created = await client.PostAsJsonAsync("/api/races", new
        {
            athleteUserId = auth.UserId, name = "Spartan Beast", sport = "Running", startsAtUtc = "2026-10-10T06:45:00Z", priority = "B", targetTimeSeconds = 9000,
        });
        var race = (await created.Content.ReadFromJsonAsync<RaceDto>(JsonOptions))!;

        var edited = await client.PutAsJsonAsync($"/api/races/{race.Id}", new
        {
            name = "Spartan Race Beast Hvar", sport = "Running", startsAtUtc = "2026-10-10T06:45:00Z", priority = "A", targetTimeSeconds = 8400, actualTimeSeconds = 8650,
        });
        edited.StatusCode.Should().Be(HttpStatusCode.OK);

        var races = await client.GetFromJsonAsync<List<RaceDto>>($"/api/athletes/{auth.UserId}/races", JsonOptions);
        races!.Single().Should().Be(new RaceDto(race.Id, "Spartan Race Beast Hvar", 8400, 8650));

        (await client.DeleteAsync($"/api/races/{race.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<List<RaceDto>>($"/api/athletes/{auth.UserId}/races", JsonOptions)).Should().BeEmpty();
    }

    [Fact]
    public async Task Athlete_edits_and_deletes_own_goal()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        var created = await client.PostAsJsonAsync("/api/goals", new { athleteUserId = auth.UserId, title = "Sub 40 na 10 km", priority = "A" });
        var goal = (await created.Content.ReadFromJsonAsync<GoalDto>(JsonOptions))!;

        (await client.PutAsJsonAsync($"/api/goals/{goal.Id}", new { title = "Sub 39 na 10 km", priority = "A", isAchieved = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<List<GoalDto>>($"/api/athletes/{auth.UserId}/goals", JsonOptions))!.Single().Title.Should().Be("Sub 39 na 10 km");

        (await client.DeleteAsync($"/api/goals/{goal.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<List<GoalDto>>($"/api/athletes/{auth.UserId}/goals", JsonOptions)).Should().BeEmpty();
    }

    [Fact]
    public async Task Coach_and_athlete_comment_on_a_race_and_the_thread_goes_with_it()
    {
        var coach = AuthenticatedClient(await RegisterAsync($"coach-{Guid.NewGuid():N}@test.cz", "Coach"));
        var athleteEmail = $"{Guid.NewGuid():N}@test.cz";
        var athleteAuth = await RegisterAsync(athleteEmail, "Athlete");
        var athlete = AuthenticatedClient(athleteAuth);
        var invite = await coach.PostAsJsonAsync("/api/relationships/invite", new { athleteEmail, note = "" });
        var relationshipId = (await invite.Content.ReadFromJsonAsync<IdDto>(JsonOptions))!.Id;
        (await athlete.PostAsJsonAsync($"/api/relationships/{relationshipId}/respond", new { accept = true })).EnsureSuccessStatusCode();

        var created = await athlete.PostAsJsonAsync("/api/races", new
        {
            athleteUserId = athleteAuth.UserId, name = "Spartan Sprint", sport = "Running", startsAtUtc = "2026-10-11T07:30:00Z", priority = "A",
        });
        var race = (await created.Content.ReadFromJsonAsync<RaceDto>(JsonOptions))!;

        (await coach.GetFromJsonAsync<RaceDto>($"/api/races/{race.Id}", JsonOptions))!.Name.Should().Be("Spartan Sprint");
        (await coach.PostAsJsonAsync("/api/comments", new { raceId = race.Id, text = "Start rozumně, poslední překážky na plno!" })).EnsureSuccessStatusCode();
        (await athlete.PostAsJsonAsync("/api/comments", new { raceId = race.Id, text = "Díky, těším se." })).EnsureSuccessStatusCode();

        var thread = (await athlete.GetFromJsonAsync<List<CommentDto>>($"/api/races/{race.Id}/comments", JsonOptions))!;
        thread.Select(c => (c.AuthorRole, c.Text)).Should().Equal(("Coach", "Start rozumně, poslední překážky na plno!"), ("Athlete", "Díky, těším se."));
        thread.Should().OnlyContain(c => c.RaceId == race.Id && c.PlannedWorkoutId == null);

        // Neither or both targets is rejected.
        (await coach.PostAsJsonAsync("/api/comments", new { text = "kam?" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await athlete.DeleteAsync($"/api/races/{race.Id}")).EnsureSuccessStatusCode();
        (await athlete.GetAsync($"/api/races/{race.Id}/comments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_stranger_cannot_read_or_comment_on_a_race()
    {
        var owner = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var created = await AuthenticatedClient(owner).PostAsJsonAsync("/api/races", new
        {
            athleteUserId = owner.UserId, name = "Můj závod", sport = "Running", startsAtUtc = "2026-10-10T06:45:00Z", priority = "B",
        });
        var race = (await created.Content.ReadFromJsonAsync<RaceDto>(JsonOptions))!;
        var coach = AuthenticatedClient(await RegisterAsync($"coach-{Guid.NewGuid():N}@test.cz", "Coach"));

        (await coach.GetAsync($"/api/races/{race.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await coach.PostAsJsonAsync("/api/comments", new { raceId = race.Id, text = "ahoj" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_athlete_cannot_delete_my_race()
    {
        var owner = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var created = await AuthenticatedClient(owner).PostAsJsonAsync("/api/races", new
        {
            athleteUserId = owner.UserId, name = "Můj závod", sport = "Running", startsAtUtc = "2026-10-10T06:45:00Z", priority = "B",
        });
        var race = (await created.Content.ReadFromJsonAsync<RaceDto>(JsonOptions))!;
        var other = AuthenticatedClient(await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"));

        (await other.DeleteAsync($"/api/races/{race.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
