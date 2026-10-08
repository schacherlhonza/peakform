using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Planning;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>The coach dashboard's one-request summary of every athlete's day.</summary>
public class CoachTodayApiTests : IntegrationTestBase
{
    private static readonly DateOnly Day = new(2026, 10, 7); // a Wednesday

    private record IdDto(Guid Id);
    private record SegmentDto(string Type, int? RepeatCount);
    private record ActualDto(string? Title, int DurationSeconds);
    private record WorkoutDto(string Title, bool IsRestDay, ActualDto? Actual, int? DurationCompliancePercent, List<SegmentDto> Segments);
    private record ReadinessDto(bool IsToday);
    private record AthleteDto(
        Guid AthleteUserId, string Name, bool WellnessShared, bool PlanShared, bool ActivitiesShared, bool HealthFlagsShared,
        ReadinessDto? Readiness, List<WorkoutDto> TodayWorkouts, List<ActualDto> UnplannedActivities, int? WeekPlannedWorkouts, int? WeekCompletedWorkouts);
    private record TodayDto(DateOnly Date, List<AthleteDto> Athletes);

    private async Task<(HttpClient Athlete, Guid AthleteId, Guid RelationshipId)> LinkAthleteAsync(HttpClient coach, string firstName)
    {
        var email = $"athlete-{Guid.NewGuid():N}@example.com";
        var auth = await RegisterAsync(email, "Athlete", firstName: firstName);
        var athlete = AuthenticatedClient(auth);
        var invite = await coach.PostAsJsonAsync("/api/relationships/invite", new { athleteEmail = email, note = "" });
        var relationshipId = (await invite.Content.ReadFromJsonAsync<IdDto>(JsonOptions))!.Id;
        (await athlete.PostAsJsonAsync($"/api/relationships/{relationshipId}/respond", new { accept = true })).EnsureSuccessStatusCode();
        return (athlete, Guid.Parse(auth.UserId), relationshipId);
    }

    [Fact]
    public async Task Summarises_each_athletes_day_within_what_they_share()
    {
        var coachAuth = await RegisterAsync($"coach-{Guid.NewGuid():N}@example.com", "Coach");
        var coach = AuthenticatedClient(coachAuth);
        var (_, anna, _) = await LinkAthleteAsync(coach, "Anna");
        var (bobClient, bob, bobRelationship) = await LinkAthleteAsync(coach, "Bob");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var monday = Day.AddDays(-2);
            var plan = new TrainingPlan { AthleteUserId = anna, CoachUserId = Guid.Parse(coachAuth.UserId), Name = "Plán", StartDate = monday, CreatedAtUtc = DateTime.UtcNow };
            var week = new TrainingWeek { TrainingPlan = plan, WeekStartDate = monday, WeekIndex = 0, CreatedAtUtc = DateTime.UtcNow };
            var block = new WorkoutSegment { Order = 2, Type = WorkoutSegmentType.Repeat, RepeatCount = 4 };
            var intervals = new PlannedWorkout
            {
                TrainingWeek = week, Date = Day, Sport = SportType.Running, Title = "Intervaly", PlannedDurationSeconds = 3000, CreatedAtUtc = DateTime.UtcNow,
                Segments =
                {
                    new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.WarmUp, DurationSeconds = 900 },
                    block,
                    new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.Interval, DurationSeconds = 180, ParentSegment = block },
                },
            };
            var yesterday = new PlannedWorkout { TrainingWeek = week, Date = Day.AddDays(-1), Sport = SportType.Running, Title = "Lehký běh", PlannedDurationSeconds = 2400, CreatedAtUtc = DateTime.UtcNow };
            db.PlannedWorkouts.AddRange(intervals, yesterday);

            var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{anna:N}-run", FetchedAtUtc = DateTime.UtcNow };
            db.CompletedActivities.Add(new CompletedActivity
            {
                AthleteUserId = anna, Sport = SportType.Running, Title = "Ranní intervaly", DurationSeconds = 2850,
                StartedAtUtc = new DateTime(2026, 10, 7, 5, 0, 0, DateTimeKind.Utc), CreatedAtUtc = DateTime.UtcNow,
                PrimarySourceRecordId = record.Id, SourceRecords = { record },
            });
            await db.SaveChangesAsync();
        }

        // Bob stops sharing his plan (and so plan vs actual) with the coach.
        (await bobClient.PutAsJsonAsync($"/api/relationships/{bobRelationship}/permissions", new { scope = "ViewTrainingPlan", granted = false })).EnsureSuccessStatusCode();

        var result = (await coach.GetFromJsonAsync<TodayDto>($"/api/coach/today?date={Day:yyyy-MM-dd}", JsonOptions))!;

        result.Date.Should().Be(Day);
        result.Athletes.Select(a => a.Name).Should().Equal("Anna User", "Bob User");

        var a = result.Athletes[0];
        a.PlanShared.Should().BeTrue();
        a.ActivitiesShared.Should().BeTrue();
        a.WellnessShared.Should().BeTrue();
        a.Readiness.Should().NotBeNull();
        a.HealthFlagsShared.Should().BeFalse("health flags aren't granted by default");
        var workout = a.TodayWorkouts.Should().ContainSingle().Subject;
        workout.Title.Should().Be("Intervaly");
        workout.Actual!.Title.Should().Be("Ranní intervaly");
        workout.DurationCompliancePercent.Should().Be(95);
        workout.Segments.Select(s => (s.Type, s.RepeatCount)).Should().Equal(("WarmUp", null), ("Repeat", 4));
        a.UnplannedActivities.Should().BeEmpty();
        (a.WeekPlannedWorkouts, a.WeekCompletedWorkouts).Should().Be((2, 1));

        var b = result.Athletes[1];
        b.PlanShared.Should().BeFalse();
        b.TodayWorkouts.Should().BeEmpty();
        b.WeekPlannedWorkouts.Should().BeNull();
        b.WellnessShared.Should().BeTrue();
        b.AthleteUserId.Should().Be(bob);
    }

    [Fact]
    public async Task Athletes_cannot_use_the_coach_summary()
    {
        var athlete = AuthenticatedClient(await RegisterAsync($"athlete-{Guid.NewGuid():N}@example.com", "Athlete"));

        (await athlete.GetAsync("/api/coach/today")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
