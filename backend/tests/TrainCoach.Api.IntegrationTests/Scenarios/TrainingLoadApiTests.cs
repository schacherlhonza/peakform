using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Wellness;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Wellness;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class TrainingLoadApiTests : IntegrationTestBase
{
    private record SnapshotDto(DateOnly Date, decimal? Ctl, decimal? Atl, string Source);

    [Fact]
    public async Task Recompute_builds_daily_peakform_series_which_takes_ctl_precedence_over_the_provider()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        var athlete = Guid.Parse(auth.UserId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            for (var i = 20; i >= 1; i--)
            {
                var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{athlete:N}-{i}", FetchedAtUtc = DateTime.UtcNow };
                db.CompletedActivities.Add(new CompletedActivity
                {
                    AthleteUserId = athlete, Sport = SportType.Running, StartedAtUtc = DateTime.UtcNow.Date.AddDays(-i).AddHours(10),
                    DurationSeconds = 3600, AverageHeartRateBpm = 150, MaxHeartRateBpm = 185,
                    CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
                });
            }
            // intervals.icu has its own value for yesterday only.
            db.TrainingLoadSnapshots.Add(new TrainingLoadSnapshot { AthleteUserId = athlete, Date = today.AddDays(-1), Ctl = 12, Atl = 34, Source = DataSource.IntervalsIcu, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITrainingLoadRecomputeJob>().RunAsync(athlete);
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            (await db.ActivityMetrics.CountAsync(m => m.Source == DataSource.PeakForm && m.MetricType == ActivityMetricType.TrainingLoad)).Should().Be(20);
        }

        var range = $"from={today.AddDays(-30):yyyy-MM-dd}&to={today:yyyy-MM-dd}";
        var merged = (await client.GetFromJsonAsync<List<SnapshotDto>>($"/api/athletes/{auth.UserId}/training-load?{range}", JsonOptions))!;
        merged.Should().OnlyHaveUniqueItems(s => s.Date, "one row per day");
        merged.Should().OnlyContain(s => s.Source == "PeakForm",
            "for CTL/ATL PeakForm wins even where intervals.icu has a value — it covers the whole history");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            (await db.DailyMetricSelections.SingleAsync(s => s.AthleteUserId == athlete && s.Date == today.AddDays(-1) && s.MetricKind == WellnessMetricKind.Ctl))
                .SelectedSource.Should().Be(DataSource.PeakForm, "the stored selection readiness reads was refreshed");
        }

        var intervalsOnly = (await client.GetFromJsonAsync<List<SnapshotDto>>($"/api/athletes/{auth.UserId}/training-load?{range}&source=IntervalsIcu", JsonOptions))!;
        intervalsOnly.Should().ContainSingle().Which.Ctl.Should().Be(12m, "the provider's own values stay stored and readable");

        var peakForm = (await client.GetFromJsonAsync<List<SnapshotDto>>($"/api/athletes/{auth.UserId}/training-load?{range}&source=PeakForm", JsonOptions))!;
        peakForm.Should().OnlyContain(s => s.Source == "PeakForm").And.HaveCount(21, "first activity day through today");
        peakForm.OrderBy(s => s.Date).Last().Ctl.Should().BeGreaterThan(0);
    }
}
