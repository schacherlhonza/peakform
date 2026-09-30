using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>The dashboard readiness card against the data shape Garmin-via-intervals.icu delivers:
/// HRV, resting HR and sleep for every day, but no provider readiness score.</summary>
public class ReadinessApiTests : IntegrationTestBase
{
    private record ComponentDto(string Factor, int SubScore);
    private record ReadinessResponse(string? Date, bool IsToday, int? Score, string? ScoreSource, decimal? HrvRmssdMs, decimal? HrvBaselineMs, List<ComponentDto> Components);

    private static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public async Task WithoutVendorScore_ComputesEstimateFromSignals()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var athleteUserId = Guid.Parse(auth.UserId);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            for (var i = 0; i <= 7; i++)
            {
                var date = Today.AddDays(-i);
                db.HrvMeasurements.Add(new HrvMeasurement { AthleteUserId = athleteUserId, Date = date, RmssdMs = i == 0 ? 43 : 48, Source = DataSource.IntervalsIcu, CreatedAtUtc = DateTime.UtcNow });
                db.RecoveryMetrics.Add(new RecoveryMetric { AthleteUserId = athleteUserId, Date = date, RestingHeartRateBpm = 52, Source = DataSource.IntervalsIcu, CreatedAtUtc = DateTime.UtcNow });
                db.SleepRecords.Add(new SleepRecord { AthleteUserId = athleteUserId, Date = date, DurationMinutes = 480, SleepScore = 86, Source = DataSource.IntervalsIcu, CreatedAtUtc = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        var client = AuthenticatedClient(auth);
        var result = await client.GetFromJsonAsync<ReadinessResponse>(
            $"/api/athletes/{athleteUserId}/readiness?date={Today:yyyy-MM-dd}", JsonOptions);

        result!.IsToday.Should().BeTrue();
        result.ScoreSource.Should().Be("Computed");
        result.Score.Should().NotBeNull().And.BeGreaterThan(0);
        result.HrvBaselineMs.Should().Be(48);
        result.Components.Select(c => c.Factor).Should().BeEquivalentTo(["Hrv", "RestingHeartRate", "Sleep"]);
    }

    [Fact]
    public async Task NoRecoveryData_ReturnsEmpty()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");

        var result = await AuthenticatedClient(auth).GetFromJsonAsync<ReadinessResponse>(
            $"/api/athletes/{auth.UserId}/readiness?date={Today:yyyy-MM-dd}", JsonOptions);

        result!.Date.Should().BeNull();
        result.Score.Should().BeNull();
    }
}
