using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class PendingDuplicateReevaluationTests : IntegrationTestBase
{
    [Fact]
    public async Task Pending_pairs_that_now_clear_the_threshold_are_merged_others_stay_pending()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var athlete = Guid.Parse(auth.UserId);
        var start = DateTime.UtcNow.Date.AddDays(-3).AddHours(16);
        Guid merged, sameSource, farApart, survivor, absorbed;

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            CompletedActivity Activity(DataSource source, DateTime utc, int seconds)
            {
                var record = new ActivitySourceRecord { Source = source, ExternalId = Guid.NewGuid().ToString("N"), FetchedAtUtc = DateTime.UtcNow };
                var a = new CompletedActivity
                {
                    AthleteUserId = athlete, Sport = SportType.Strength, StartedAtUtc = utc, DurationSeconds = seconds,
                    CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
                };
                db.CompletedActivities.Add(a);
                return a;
            }
            DuplicateCandidate Pair(CompletedActivity a, CompletedActivity b)
            {
                var c = new DuplicateCandidate { AthleteUserId = athlete, ActivityAId = a.Id, ActivityBId = b.Id, ConfidenceScore = 75, CreatedAtUtc = DateTime.UtcNow };
                db.DuplicateCandidates.Add(c);
                return c;
            }

            // Flagged under the old 2 % rule: same start second, providers disagree on moving time by 3.5 %.
            var viaIntervals = Activity(DataSource.IntervalsIcu, start, 1590);
            var viaStrava = Activity(DataSource.Strava, start, 1645);
            survivor = viaIntervals.Id;
            absorbed = viaStrava.Id;
            merged = Pair(viaIntervals, viaStrava).Id;
            // Two Strava activities are two real sessions, however alike.
            sameSource = Pair(Activity(DataSource.Strava, start.AddDays(1), 1800), Activity(DataSource.Strava, start.AddDays(1), 1800)).Id;
            // Forty minutes apart: stays for the athlete to decide.
            farApart = Pair(Activity(DataSource.IntervalsIcu, start.AddDays(2), 1800), Activity(DataSource.Strava, start.AddDays(2).AddMinutes(40), 1800)).Id;
            await db.SaveChangesAsync();
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var count = await scope.ServiceProvider.GetRequiredService<IPendingDuplicateReevaluationJob>().RunAsync(athlete);
            count.Should().Be(1);

            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var candidates = await db.DuplicateCandidates.Where(c => c.AthleteUserId == athlete).ToDictionaryAsync(c => c.Id);
            candidates[merged].Status.Should().Be(DuplicateCandidateStatus.MergedIntoA);
            candidates[sameSource].Status.Should().Be(DuplicateCandidateStatus.Pending);
            candidates[farApart].Status.Should().Be(DuplicateCandidateStatus.Pending);

            var activities = await db.CompletedActivities.IgnoreQueryFilters().Include(a => a.SourceRecords)
                .Where(a => a.Id == survivor || a.Id == absorbed).ToDictionaryAsync(a => a.Id);
            activities[absorbed].IsDeleted.Should().BeTrue();
            activities[survivor].SourceRecords.Select(s => s.Source).Should().BeEquivalentTo([DataSource.IntervalsIcu, DataSource.Strava]);
            (await db.MergeDecisions.CountAsync(m => m.SurvivingActivityId == survivor && m.Kind == MergeDecisionKind.AutoHighConfidence)).Should().Be(1);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<IPendingDuplicateReevaluationJob>().RunAsync(athlete)).Should().Be(0, "idempotent");
        }
    }
}
