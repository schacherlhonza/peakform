using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class RelationshipAndIsolationTests : IntegrationTestBase
{
    private record RelationshipDto(string Id, string Status, string AthleteUserId, string CoachUserId);
    private record PlanDto(string Id, string AthleteUserId);

    [Fact]
    public async Task Coach_CannotAccessAthletePlan_WithoutActiveRelationship()
    {
        var coach = await RegisterAsync($"coach-idor-{Guid.NewGuid():N}@example.com", "Coach");
        var athlete = await RegisterAsync($"athlete-idor-{Guid.NewGuid():N}@example.com", "Athlete");

        var coachClient = AuthenticatedClient(coach);

        // No invite/relationship exists at all — the coach must not be able to read this athlete's
        // plans just by knowing their user id.
        var response = await coachClient.GetAsync($"/api/athletes/{athlete.UserId}/plans");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Coach_CannotAccessAthleteCheckIns_WithoutActiveRelationship()
    {
        var coach = await RegisterAsync($"coach-idor2-{Guid.NewGuid():N}@example.com", "Coach");
        var athlete = await RegisterAsync($"athlete-idor2-{Guid.NewGuid():N}@example.com", "Athlete");
        var coachClient = AuthenticatedClient(coach);

        var response = await coachClient.GetAsync($"/api/athletes/{athlete.UserId}/checkins?from=2026-01-01&to=2026-12-31");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Athlete_CannotAccessAnotherAthletesData()
    {
        var athleteA = await RegisterAsync($"athlete-a-{Guid.NewGuid():N}@example.com", "Athlete");
        var athleteB = await RegisterAsync($"athlete-b-{Guid.NewGuid():N}@example.com", "Athlete");
        var clientA = AuthenticatedClient(athleteA);

        var response = await clientA.GetAsync($"/api/athletes/{athleteB.UserId}/plans");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FullVerticalFlow_InviteAcceptPlanCheckInComment_Works()
    {
        var coach = await RegisterAsync($"coach-flow-{Guid.NewGuid():N}@example.com", "Coach");
        var athleteEmail = $"athlete-flow-{Guid.NewGuid():N}@example.com";
        var athlete = await RegisterAsync(athleteEmail, "Athlete");

        var coachClient = AuthenticatedClient(coach);
        var athleteClient = AuthenticatedClient(athlete);

        // 1. Coach invites athlete.
        var inviteResponse = await coachClient.PostAsJsonAsync("/api/relationships/invite", new { athleteEmail, note = "Vítej v týmu!" });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var relationship = (await inviteResponse.Content.ReadFromJsonAsync<RelationshipDto>(JsonOptions))!;
        relationship.Status.Should().Be("PendingInvite");

        // Before acceptance, the coach still has no access.
        var beforeAccept = await coachClient.GetAsync($"/api/athletes/{athlete.UserId}/plans");
        beforeAccept.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Athlete accepts.
        var respondResponse = await athleteClient.PostAsJsonAsync($"/api/relationships/{relationship.Id}/respond", new { accept = true });
        respondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = (await respondResponse.Content.ReadFromJsonAsync<RelationshipDto>(JsonOptions))!;
        accepted.Status.Should().Be("Active");

        // 3. Coach creates a training plan for the athlete.
        var createPlanResponse = await coachClient.PostAsJsonAsync("/api/plans", new
        {
            athleteUserId = athlete.UserId,
            name = "Jarní příprava",
            startDate = "2026-01-05",
        });
        createPlanResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var plan = (await createPlanResponse.Content.ReadFromJsonAsync<PlanDto>(JsonOptions))!;

        // 4. Athlete can see their own plan.
        var athletePlansResponse = await athleteClient.GetAsync($"/api/athletes/{athlete.UserId}/plans");
        athletePlansResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var athletePlans = await athletePlansResponse.Content.ReadFromJsonAsync<List<PlanDto>>(JsonOptions);
        athletePlans.Should().ContainSingle(p => p.Id == plan.Id);

        // Coach now also has access, since the relationship is active with default scopes.
        var coachPlansResponse = await coachClient.GetAsync($"/api/athletes/{athlete.UserId}/plans");
        coachPlansResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Athlete submits a morning check-in.
        var checkInResponse = await athleteClient.PutAsJsonAsync("/api/checkins", new
        {
            athleteUserId = athlete.UserId,
            date = "2026-01-05",
            type = "Morning",
            energy = "Good",
            sleepQuality = "Good",
        });
        checkInResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Revoke access — coach loses it again.
        var revokeResponse = await athleteClient.PostAsJsonAsync($"/api/relationships/{relationship.Id}/revoke", new { reason = "Konec spolupráce" });
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRevoke = await coachClient.GetAsync($"/api/athletes/{athlete.UserId}/plans");
        afterRevoke.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Coach_CannotCreatePlan_ForAthleteWithoutActiveRelationship()
    {
        var coach = await RegisterAsync($"coach-noplan-{Guid.NewGuid():N}@example.com", "Coach");
        var athlete = await RegisterAsync($"athlete-noplan-{Guid.NewGuid():N}@example.com", "Athlete");
        var coachClient = AuthenticatedClient(coach);

        var response = await coachClient.PostAsJsonAsync("/api/plans", new
        {
            athleteUserId = athlete.UserId,
            name = "Neoprávněný plán",
            startDate = "2026-01-05",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Athlete_CannotCreatePlan_EvenForThemselves()
    {
        var athlete = await RegisterAsync($"athlete-selfplan-{Guid.NewGuid():N}@example.com", "Athlete");
        var athleteClient = AuthenticatedClient(athlete);

        var response = await athleteClient.PostAsJsonAsync("/api/plans", new
        {
            athleteUserId = athlete.UserId,
            name = "Vlastní plán",
            startDate = "2026-01-05",
        });

        // Only a coach may create/edit a plan — role-gated at the controller.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
