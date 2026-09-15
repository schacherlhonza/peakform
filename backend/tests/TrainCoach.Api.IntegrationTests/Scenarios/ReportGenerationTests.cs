using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class ReportGenerationTests : IntegrationTestBase
{
    private record ReportDto(string Id, string AthleteUserId, string NarrativeText, List<object> Insights);

    [Fact]
    public async Task GetReport_WhenNoneExistsYet_LazilyGeneratesOne()
    {
        var athlete = await RegisterAsync($"report-{Guid.NewGuid():N}@example.com", "Athlete");
        var client = AuthenticatedClient(athlete);

        var response = await client.GetAsync($"/api/athletes/{athlete.UserId}/reports/2026-01-05/Morning");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = (await response.Content.ReadFromJsonAsync<ReportDto>(JsonOptions))!;
        report.NarrativeText.Should().NotBeNullOrWhiteSpace();
        report.NarrativeText.Should().Contain("lékařské");
    }

    [Fact]
    public async Task Athlete_CannotReadAnotherAthletesReport()
    {
        var athleteA = await RegisterAsync($"report-a-{Guid.NewGuid():N}@example.com", "Athlete");
        var athleteB = await RegisterAsync($"report-b-{Guid.NewGuid():N}@example.com", "Athlete");
        var clientA = AuthenticatedClient(athleteA);

        var response = await clientA.GetAsync($"/api/athletes/{athleteB.UserId}/reports/2026-01-05/Morning");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EveningCheckIn_AutomaticallyTriggersEveningReportGeneration()
    {
        var athlete = await RegisterAsync($"report-evening-{Guid.NewGuid():N}@example.com", "Athlete");
        var client = AuthenticatedClient(athlete);

        var checkInResponse = await client.PutAsJsonAsync("/api/checkins", new
        {
            athleteUserId = athlete.UserId,
            date = "2026-01-05",
            type = "Evening",
            energy = "Poor",
            fatigue = "Poor",
            completedPlannedWorkout = true,
            rpe = 6,
        });
        checkInResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // The background job queue is in-process; give it a moment to drain.
        await Task.Delay(500);

        var reportResponse = await client.GetAsync($"/api/athletes/{athlete.UserId}/reports/2026-01-05/Evening");
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
