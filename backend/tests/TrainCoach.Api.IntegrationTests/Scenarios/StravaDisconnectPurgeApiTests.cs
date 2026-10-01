using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Integrations;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// Strava API Agreement: disconnecting Strava permanently deletes data obtained through the API,
/// while the athlete's own data-archive import stays.
/// </summary>
public class StravaDisconnectPurgeApiTests : IntegrationTestBase
{
    private record ImpactDto(int ActivitiesDeleted, int SourcesRemoved, int KeptFromArchive);

    private static readonly DateTime Day = new(2026, 9, 1, 7, 0, 0, DateTimeKind.Utc);

    private sealed record Seeded(Guid ApiOnly, Guid Merged, Guid IntervalsRecord, Guid ApiWithArchiveStream, Guid ArchiveOnly, Guid SoftDeleted, Guid Feedback);

    private async Task<Seeded> SeedAsync(Guid athleteUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();

        CompletedActivity Activity(int hour, params ActivitySourceRecord[] records)
        {
            var activity = new CompletedActivity
            {
                AthleteUserId = athleteUserId, Sport = SportType.Running, Title = $"Běh {hour}", StartedAtUtc = Day.AddHours(hour),
                DurationSeconds = 1800, DistanceMeters = 5000, CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = records[0].Id,
                MatchStatus = records.Length > 1 ? ActivityMatchStatus.AutoMerged : ActivityMatchStatus.Unambiguous,
            };
            foreach (var r in records) activity.SourceRecords.Add(r);
            db.CompletedActivities.Add(activity);
            return activity;
        }
        ActivitySourceRecord Record(DataSource source, string id, Guid? archiveImportId = null) => new()
        {
            Source = source, ExternalId = $"{athleteUserId:N}-{id}", FetchedAtUtc = DateTime.UtcNow,
            StravaArchiveImportId = archiveImportId, RawPayloadRetained = true, RawPayloadJson = "{\"api\":true}",
        };

        var archiveImport = new StravaArchiveImport
        {
            AthleteUserId = athleteUserId, Status = StravaArchiveImportStatus.Succeeded, SourceKind = StravaArchiveSourceKind.Upload,
            CreatedAtUtc = DateTime.UtcNow, FinishedAtUtc = DateTime.UtcNow,
        };
        db.StravaArchiveImports.Add(archiveImport);

        var apiOnly = Activity(0, Record(DataSource.Strava, "api"));
        db.ActivityMetrics.Add(new ActivityMetric { CompletedActivity = apiOnly, MetricType = ActivityMetricType.ElapsedTimeSeconds, Value = 1900, Unit = "s", Source = DataSource.Strava, RecordedAtUtc = DateTime.UtcNow });
        var feedback = new TrainingFeedback { AthleteUserId = athleteUserId, CompletedActivityId = apiOnly.Id, Date = DateOnly.FromDateTime(Day), Rpe = 6 };
        db.TrainingFeedbacks.Add(feedback);

        var mergedStrava = Record(DataSource.Strava, "merged");
        var intervals = Record(DataSource.IntervalsIcu, "i1");
        var merged = Activity(2, mergedStrava, intervals);
        db.MergeDecisions.Add(new MergeDecision
        {
            AthleteUserId = athleteUserId, SurvivingActivityId = merged.Id, AbsorbedSourceRecordId = mergedStrava.Id, ConfidenceScore = 95,
            Kind = MergeDecisionKind.AutoHighConfidence, Outcome = MergeDecisionOutcome.Merged, DecidedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow,
        });

        var withArchiveStream = Record(DataSource.Strava, "api-archive-stream");
        var apiWithArchiveStream = Activity(4, withArchiveStream);
        var builder = new ActivityStreamBuilder();
        builder.Add(new ActivityStreamBuilder.Sample(Day, HeartRate: 120));
        builder.Add(new ActivityStreamBuilder.Sample(Day.AddSeconds(1), HeartRate: 121));
        db.ActivityStreams.Add(ActivityStreamMapping.ToEntity(withArchiveStream.Id, builder.Build()!, ActivityStreamOrigin.StravaArchive, DateTime.UtcNow));

        var archiveOnly = Activity(6, Record(DataSource.Strava, "archive", archiveImport.Id));

        var softDeleted = Activity(8, Record(DataSource.Strava, "deleted"));
        softDeleted.IsDeleted = true;

        db.DuplicateCandidates.Add(new DuplicateCandidate
        {
            AthleteUserId = athleteUserId, ActivityAId = merged.Id, ActivityBId = apiOnly.Id, ConfidenceScore = 70, ScoringBreakdownJson = "{}",
            Status = DuplicateCandidateStatus.Pending, CreatedAtUtc = DateTime.UtcNow,
        });

        db.IntegrationConnections.Add(new IntegrationConnection
        {
            AthleteUserId = athleteUserId, Provider = IntegrationProviderType.Strava, Status = IntegrationConnectionStatus.Connected,
            ConnectedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return new Seeded(apiOnly.Id, merged.Id, intervals.Id, apiWithArchiveStream.Id, archiveOnly.Id, softDeleted.Id, feedback.Id);
    }

    [Fact]
    public async Task Disconnect_deletes_strava_api_data_and_keeps_archive_and_other_sources()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        var seeded = await SeedAsync(Guid.Parse(auth.UserId));

        var impact = (await client.GetFromJsonAsync<ImpactDto>("/api/integrations/Strava/disconnect-impact", JsonOptions))!;
        impact.Should().Be(new ImpactDto(ActivitiesDeleted: 2, SourcesRemoved: 1, KeptFromArchive: 1));

        (await client.DeleteAsync("/api/integrations/Strava")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
        var remaining = await db.CompletedActivities.IgnoreQueryFilters().Include(a => a.SourceRecords)
            .Where(a => a.AthleteUserId == Guid.Parse(auth.UserId)).ToListAsync();

        remaining.Select(a => a.Id).Should().BeEquivalentTo([seeded.Merged, seeded.ApiWithArchiveStream, seeded.ArchiveOnly],
            "the API-only activity is gone and so is the soft-deleted one — hard delete");

        var merged = remaining.Single(a => a.Id == seeded.Merged);
        merged.SourceRecords.Should().ContainSingle().Which.Source.Should().Be(DataSource.IntervalsIcu);
        merged.PrimarySourceRecordId.Should().Be(seeded.IntervalsRecord);
        merged.MatchStatus.Should().Be(ActivityMatchStatus.Unambiguous);

        var retagged = remaining.Single(a => a.Id == seeded.ApiWithArchiveStream).SourceRecords.Single();
        retagged.StravaArchiveImportId.Should().NotBeNull("the archive holds the same activity");
        retagged.RawPayloadJson.Should().BeNull("the raw API response is Strava API data");
        (await db.ActivityStreams.CountAsync(s => s.ActivitySourceRecordId == retagged.Id)).Should().Be(1);

        (await db.ActivityMetrics.CountAsync(m => m.CompletedActivityId == seeded.ApiOnly)).Should().Be(0);
        (await db.MergeDecisions.CountAsync(m => m.SurvivingActivityId == seeded.Merged)).Should().Be(0);
        (await db.DuplicateCandidates.CountAsync(c => c.AthleteUserId == Guid.Parse(auth.UserId))).Should().Be(0);
        (await db.TrainingFeedbacks.SingleAsync(f => f.Id == seeded.Feedback)).CompletedActivityId.Should().BeNull("the athlete's own feedback stays");
    }

    [Fact]
    public async Task Disconnecting_another_provider_deletes_nothing()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        await SeedAsync(Guid.Parse(auth.UserId));

        var impact = (await client.GetFromJsonAsync<ImpactDto>("/api/integrations/IntervalsIcu/disconnect-impact", JsonOptions))!;

        impact.Should().Be(new ImpactDto(0, 0, 0));
    }
}
