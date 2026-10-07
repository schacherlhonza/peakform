using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Planning;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class PlanVsActualApiTests : IntegrationTestBase
{
    private record ActualDto(Guid ActivityId, string? Title, int DurationSeconds, List<int> ZoneSeconds, int BelowZonesSeconds);
    private record WorkoutDto(string Title, bool IsRestDay, List<int> PlannedZoneSeconds, int PlannedUnspecifiedSeconds, ActualDto? Actual, bool SportMismatch, int? DurationCompliancePercent, int? DistanceCompliancePercent);
    private record DayDto(DateOnly Date, List<WorkoutDto> Workouts, List<ActualDto> UnplannedActivities);
    private record TotalsDto(int PlannedDurationSeconds, int ActualDurationSeconds, List<int> PlannedZoneSeconds, List<int> ActualZoneSeconds, int PlannedWorkouts, int CompletedWorkouts);
    private record ResultDto(bool ActualAvailable, List<DayDto> Days, TotalsDto Totals);

    private static readonly DateOnly Monday = new(2026, 9, 28);

    [Fact]
    public async Task Pairs_workouts_with_same_day_same_sport_activities_and_compares_zones()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"); // Europe/Prague
        var athlete = Guid.Parse(auth.UserId);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var z2 = new HeartRateZone { AthleteUserId = athlete, ZoneNumber = 2, Name = "Z2", MinBpm = 145, MaxBpm = 155, EffectiveFromDate = new DateOnly(2025, 1, 1), CreatedAtUtc = DateTime.UtcNow };
            var z4 = new HeartRateZone { AthleteUserId = athlete, ZoneNumber = 4, Name = "Z4", MinBpm = 162, MaxBpm = 167, EffectiveFromDate = new DateOnly(2025, 1, 1), CreatedAtUtc = DateTime.UtcNow };
            db.HeartRateZones.AddRange(z2, z4);

            var plan = new TrainingPlan { AthleteUserId = athlete, CoachUserId = athlete, Name = "Plán", StartDate = Monday, CreatedAtUtc = DateTime.UtcNow };
            var week = new TrainingWeek { TrainingPlan = plan, WeekStartDate = Monday, WeekIndex = 0, CreatedAtUtc = DateTime.UtcNow };
            var intervals = new PlannedWorkout
            {
                TrainingWeek = week, Date = Monday, Sport = SportType.Running, Title = "Intervaly", PlannedDurationSeconds = 3600, PlannedDistanceMeters = 10000, CreatedAtUtc = DateTime.UtcNow,
                Segments =
                {
                    new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.WarmUp, DurationSeconds = 900, IntensityTargetType = IntensityTargetType.HeartRateZone, TargetHeartRateZoneId = z2.Id },
                    new WorkoutSegment { Order = 2, Type = WorkoutSegmentType.Interval, DurationSeconds = 300, RepeatCount = 6, IntensityTargetType = IntensityTargetType.HeartRateZone, TargetHeartRateZoneNumber = 4 },
                    new WorkoutSegment { Order = 3, Type = WorkoutSegmentType.CoolDown, DurationSeconds = 900, IntensityTargetType = IntensityTargetType.Free },
                },
            };
            var rest = new PlannedWorkout { TrainingWeek = week, Date = Monday.AddDays(1), Sport = SportType.Rest, Title = "Volno", IsRestDay = true, CreatedAtUtc = DateTime.UtcNow };
            var missed = new PlannedWorkout { TrainingWeek = week, Date = Monday.AddDays(2), Sport = SportType.Running, Title = "Lehký běh", PlannedDurationSeconds = 2400, CreatedAtUtc = DateTime.UtcNow };
            // Thursday: a run is planned, the athlete did strength instead — still paired, flagged.
            var swapped = new PlannedWorkout { TrainingWeek = week, Date = Monday.AddDays(3), Sport = SportType.Running, Title = "Tempo", PlannedDurationSeconds = 1800, CreatedAtUtc = DateTime.UtcNow };
            db.PlannedWorkouts.AddRange(intervals, rest, missed, swapped);

            CompletedActivity Activity(string title, SportType sport, DateTime utc, int seconds, decimal? meters)
            {
                var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{athlete:N}-{title}", FetchedAtUtc = DateTime.UtcNow };
                var a = new CompletedActivity
                {
                    AthleteUserId = athlete, Sport = sport, Title = title, StartedAtUtc = utc, DurationSeconds = seconds, DistanceMeters = meters,
                    CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
                };
                db.CompletedActivities.Add(a);
                return a;
            }
            // Monday 00:30 Prague = Sunday 22:30 UTC — still belongs to Monday.
            var run = Activity("Ranní intervaly", SportType.Running, new DateTime(2026, 9, 27, 22, 30, 0, DateTimeKind.Utc), 3300, 9500);
            db.ActivityMetrics.AddRange(
                new ActivityMetric { CompletedActivity = run, MetricType = ActivityMetricType.TimeInHrZone2, Value = 1500, Unit = "s", Source = DataSource.PeakForm, RecordedAtUtc = DateTime.UtcNow },
                new ActivityMetric { CompletedActivity = run, MetricType = ActivityMetricType.TimeInHrZone4, Value = 1600, Unit = "s", Source = DataSource.PeakForm, RecordedAtUtc = DateTime.UtcNow },
                new ActivityMetric { CompletedActivity = run, MetricType = ActivityMetricType.TimeBelowHrZones, Value = 200, Unit = "s", Source = DataSource.PeakForm, RecordedAtUtc = DateTime.UtcNow });
            Activity("Posilovna", SportType.Strength, new DateTime(2026, 9, 28, 16, 0, 0, DateTimeKind.Utc), 1800, null);
            Activity("Síla", SportType.Strength, new DateTime(2026, 10, 1, 16, 0, 0, DateTimeKind.Utc), 1500, null);
            await db.SaveChangesAsync();
        }

        var result = (await AuthenticatedClient(auth).GetFromJsonAsync<ResultDto>(
            $"/api/athletes/{auth.UserId}/plan-vs-actual?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}", JsonOptions))!;

        result.ActualAvailable.Should().BeTrue();
        result.Days.Should().HaveCount(7);
        var monday = result.Days[0];
        var workout = monday.Workouts.Single();
        workout.Actual!.Title.Should().Be("Ranní intervaly");
        workout.DurationCompliancePercent.Should().Be(92);
        workout.DistanceCompliancePercent.Should().Be(95);
        workout.PlannedZoneSeconds.Should().Equal(0, 900, 0, 1800, 0, 0, 0);
        workout.PlannedUnspecifiedSeconds.Should().Be(900);
        workout.Actual.ZoneSeconds.Should().Equal(0, 1500, 0, 1600, 0, 0, 0);
        workout.Actual.BelowZonesSeconds.Should().Be(200);
        workout.SportMismatch.Should().BeFalse();
        monday.UnplannedActivities.Should().ContainSingle(a => a.Title == "Posilovna", "the run already has its same-sport activity");

        result.Days[1].Workouts.Single().Should().Match<WorkoutDto>(w => w.IsRestDay && w.Actual == null);
        result.Days[2].Workouts.Single().Should().Match<WorkoutDto>(w => w.Actual == null && w.DurationCompliancePercent == null);
        result.Days[3].Workouts.Single().Should().Match<WorkoutDto>(w => w.Actual != null && w.Actual.Title == "Síla" && w.SportMismatch);
        result.Days[3].UnplannedActivities.Should().BeEmpty();

        result.Totals.PlannedWorkouts.Should().Be(3);
        result.Totals.CompletedWorkouts.Should().Be(2);
        result.Totals.PlannedDurationSeconds.Should().Be(7800);
        result.Totals.ActualDurationSeconds.Should().Be(6600);
    }
}
