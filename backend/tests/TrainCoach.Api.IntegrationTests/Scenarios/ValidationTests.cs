using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class ValidationTests : IntegrationTestBase
{
    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsProblemDetailsWithFieldErrors()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "Heslo123",
            firstName = "Test",
            lastName = "User",
            role = "Athlete",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Email");
    }

    [Fact]
    public async Task SubmitCheckIn_WithRpeOutOfRange_ReturnsBadRequest()
    {
        var athlete = await RegisterAsync($"validation-{Guid.NewGuid():N}@example.com", "Athlete");
        var client = AuthenticatedClient(athlete);

        var response = await client.PutAsJsonAsync("/api/checkins", new
        {
            athleteUserId = athlete.UserId,
            date = "2026-01-05",
            type = "Evening",
            rpe = 15,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InviteAthlete_WithNonExistentEmail_ReturnsNotFound()
    {
        var coach = await RegisterAsync($"coach-invite-{Guid.NewGuid():N}@example.com", "Coach");
        var client = AuthenticatedClient(coach);

        var response = await client.PostAsJsonAsync("/api/relationships/invite", new
        {
            athleteEmail = "does-not-exist@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
