using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Infrastructure.Persistence;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>Exercises the full demo data seeder against a real (SQLite) schema — this touches
/// almost every entity in the model, so it's a strong end-to-end sanity check on its own,
/// independent of the unit-level tests elsewhere.</summary>
public class DemoDataSeederTests : IntegrationTestBase
{
    [Fact]
    public async Task SeedAsync_PopulatesDemoDataAndDemoCoachCanLogIn()
    {
        await DemoDataSeeder.SeedAsync(Factory.Services);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { email = "kouc@demo.traincoach.cz", password = "Demo1234" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
        auth.Role.Should().Be("Coach");

        var coachClient = AuthenticatedClient(auth);
        var relationshipsResponse = await coachClient.GetAsync("/api/relationships");
        relationshipsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var relationships = await relationshipsResponse.Content.ReadFromJsonAsync<List<object>>(JsonOptions);
        relationships.Should().HaveCount(2);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await DemoDataSeeder.SeedAsync(Factory.Services);
        var act = async () => await DemoDataSeeder.SeedAsync(Factory.Services);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SeedAsync_AthleteHasTrainingPlanWithFourWeeks()
    {
        await DemoDataSeeder.SeedAsync(Factory.Services);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { email = "jakub@demo.traincoach.cz", password = "Demo1234" });
        var auth = (await loginResponse.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
        var athleteClient = AuthenticatedClient(auth);

        var plansResponse = await athleteClient.GetAsync($"/api/athletes/{auth.UserId}/plans");
        plansResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await plansResponse.Content.ReadFromJsonAsync<List<PlanSummary>>(JsonOptions);
        plans.Should().ContainSingle();

        var detailResponse = await athleteClient.GetAsync($"/api/plans/{plans![0].Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<PlanDetail>(JsonOptions);
        detail!.Weeks.Should().HaveCount(4);
        detail.Weeks.SelectMany(w => w.Workouts).Should().HaveCountGreaterOrEqualTo(28);
    }

    private record PlanSummary(string Id);
    private record PlanDetail(List<WeekSummary> Weeks);
    private record WeekSummary(List<object> Workouts);
}
