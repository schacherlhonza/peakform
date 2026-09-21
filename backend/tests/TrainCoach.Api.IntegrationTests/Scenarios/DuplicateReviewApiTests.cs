using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>HTTP-level coverage of the duplicate-review endpoints: merge/dismiss/revert round-trip,
/// cross-athlete authorization isolation, and that a merge never physically deletes a row.</summary>
public class DuplicateReviewApiTests : IntegrationTestBase
{
    private async Task<Guid> SeedPendingCandidateAsync(Guid athleteUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var start = new DateTime(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc);
        var recordA = new ActivitySourceRecord { Source = DataSource.IntervalsIcu, ExternalId = "ivl-a", FetchedAtUtc = start };
        var activityA = new CompletedActivity
        {
            AthleteUserId = athleteUserId, Sport = SportType.Running, StartedAtUtc = start, DurationSeconds = 3600, DistanceMeters = 10000,
            CreatedAtUtc = start, PrimarySourceRecordId = recordA.Id, SourceRecords = { recordA },
        };
        var recordB = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = "strava-b", FetchedAtUtc = start };
        var activityB = new CompletedActivity
        {
            AthleteUserId = athleteUserId, Sport = SportType.Running, StartedAtUtc = start.AddSeconds(5), DurationSeconds = 3605, DistanceMeters = 10010,
            CreatedAtUtc = start, MatchStatus = ActivityMatchStatus.PendingReview, PrimarySourceRecordId = recordB.Id, SourceRecords = { recordB },
        };
        db.CompletedActivities.AddRange(activityA, activityB);

        var candidate = new DuplicateCandidate
        {
            AthleteUserId = athleteUserId, ActivityAId = activityA.Id, ActivityBId = activityB.Id,
            ConfidenceScore = 70, ScoringBreakdownJson = "{}", Status = DuplicateCandidateStatus.Pending, CreatedAtUtc = start,
        };
        db.DuplicateCandidates.Add(candidate);

        await db.SaveChangesAsync();
        return candidate.Id;
    }

    [Fact]
    public async Task PendingCandidate_VisibleOnlyToOwningAthlete()
    {
        var athleteA = await RegisterAsync("dup-a@test.cz", "Athlete");
        var athleteB = await RegisterAsync("dup-b@test.cz", "Athlete");
        await SeedPendingCandidateAsync(Guid.Parse(athleteA.UserId));

        var enumAwareJsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        enumAwareJsonOptions.Converters.Add(new JsonStringEnumConverter());

        var ownClient = AuthenticatedClient(athleteA);
        var ownResponse = await ownClient.GetFromJsonAsync<List<DuplicateCandidateDto>>($"/api/athletes/{athleteA.UserId}/duplicate-candidates", enumAwareJsonOptions);
        ownResponse.Should().ContainSingle();

        var otherClient = AuthenticatedClient(athleteB);
        var forbidden = await otherClient.GetAsync($"/api/athletes/{athleteA.UserId}/duplicate-candidates");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Merge_SoftDeletesLosingActivity_NeverPhysically_AndIsRevertible()
    {
        var athlete = await RegisterAsync("dup-merge@test.cz", "Athlete");
        var athleteUserId = Guid.Parse(athlete.UserId);
        var candidateId = await SeedPendingCandidateAsync(athleteUserId);

        Guid activityAId, activityBId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var candidate = await db.DuplicateCandidates.SingleAsync(c => c.Id == candidateId);
            activityAId = candidate.ActivityAId;
            activityBId = candidate.ActivityBId;
        }

        var client = AuthenticatedClient(athlete);
        var mergeResponse = await client.PostAsJsonAsync($"/api/duplicate-candidates/{candidateId}/merge", new MergeDuplicateCandidateRequest(activityAId));
        mergeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid mergeDecisionId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            // The losing activity must still exist physically (soft-delete only) — bypass the
            // global IsDeleted query filter to prove it wasn't hard-deleted.
            var losingRaw = await db.CompletedActivities.IgnoreQueryFilters().SingleAsync(a => a.Id == activityBId);
            losingRaw.IsDeleted.Should().BeTrue();

            var survivingActivity = await db.CompletedActivities.Include(a => a.SourceRecords).SingleAsync(a => a.Id == activityAId);
            survivingActivity.SourceRecords.Should().HaveCount(2, "the losing activity's source record must be re-pointed onto the survivor");
            survivingActivity.MatchStatus.Should().Be(ActivityMatchStatus.Confirmed);

            var decision = await db.MergeDecisions.SingleAsync(d => d.SurvivingActivityId == activityAId);
            decision.Outcome.Should().Be(MergeDecisionOutcome.Merged);
            mergeDecisionId = decision.Id;

            var candidate = await db.DuplicateCandidates.SingleAsync(c => c.Id == candidateId);
            candidate.Status.Should().Be(DuplicateCandidateStatus.MergedIntoA);
        }

        var revertResponse = await client.PostAsync($"/api/merge-decisions/{mergeDecisionId}/revert", null);
        revertResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var original = await db.MergeDecisions.SingleAsync(d => d.Id == mergeDecisionId);
            original.Outcome.Should().Be(MergeDecisionOutcome.Reverted, "the original decision row is updated, never deleted");

            var revertedCount = await db.CompletedActivities.CountAsync(a => a.AthleteUserId == athleteUserId && a.MatchStatus == ActivityMatchStatus.Reverted);
            revertedCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task Dismiss_KeepsBothActivitiesSeparate()
    {
        var athlete = await RegisterAsync("dup-dismiss@test.cz", "Athlete");
        var candidateId = await SeedPendingCandidateAsync(Guid.Parse(athlete.UserId));

        var client = AuthenticatedClient(athlete);
        var response = await client.PostAsync($"/api/duplicate-candidates/{candidateId}/dismiss", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var candidate = await db.DuplicateCandidates.SingleAsync(c => c.Id == candidateId);
        candidate.Status.Should().Be(DuplicateCandidateStatus.DismissedAsDistinct);

        var activities = await db.CompletedActivities.Where(a => a.Id == candidate.ActivityAId || a.Id == candidate.ActivityBId).ToListAsync();
        activities.Should().OnlyContain(a => !a.IsDeleted);
    }
}
