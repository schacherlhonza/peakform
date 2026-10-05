using FluentAssertions;
using TrainCoach.Application.Integrations;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using Xunit;

namespace TrainCoach.Application.Tests.Integrations.Matching;

/// <summary>
/// Pure scoring unit tests for ActivityMatchingService.Score (internal, exposed to this test
/// project via InternalsVisibleTo) — no database involved. Level-5 confidence scoring only; the
/// multi-candidate tie-break and FIT-identity short-circuit live in FindOrScoreMatchAsync and are
/// covered by DB-backed scenario tests instead (see Api.IntegrationTests).
/// </summary>
public class ActivityMatchingServiceScoringTests
{
    private static readonly ActivityMatchingOptions Options = new();
    private static readonly DateTime BaseStart = new(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc);

    private static CompletedActivity Candidate(SportType sport, DateTime startedAtUtc, int durationSeconds, decimal? distanceMeters) =>
        new() { Sport = sport, StartedAtUtc = startedAtUtc, DurationSeconds = durationSeconds, DistanceMeters = distanceMeters };

    private static ExternalActivity Incoming(SportType sport, DateTime startedAtUtc, int durationSeconds, decimal? distanceMeters, string? title = null, string? deviceName = null) =>
        new("ext-1", sport, title, startedAtUtc, durationSeconds, distanceMeters, null, null, null, null, null, null, null, DeviceName: deviceName);

    [Fact]
    public void SameSportSameTimeSameDistance_ScoresAboveAutoMergeThreshold()
    {
        var candidate = Candidate(SportType.Running, BaseStart, 3600, 10000);
        var incoming = Incoming(SportType.Running, BaseStart.AddSeconds(20), 3610, 10020);

        var (score, _) = ActivityMatchingService.Score(candidate, null, incoming, Options);

        score.Should().BeGreaterThanOrEqualTo(Options.AutoMergeThreshold);
    }

    [Fact]
    public void DifferentSport_AlwaysScoresZero_RegardlessOfOtherSignals()
    {
        var candidate = Candidate(SportType.Running, BaseStart, 3600, 10000);
        var incoming = Incoming(SportType.Cycling, BaseStart, 3600, 10000, deviceName: "Garmin Forerunner 965");

        var (score, breakdown) = ActivityMatchingService.Score(candidate, "Garmin Forerunner 965", incoming, Options);

        score.Should().Be(0);
        breakdown.Should().ContainKey("disqualifiedBySport");
    }

    [Fact]
    public void WarmupThenRace_SameWindow_DoesNotAutoMerge()
    {
        // Same sport, close start time, but a 12-minute warm-up jog vs. a 45-minute race — very
        // different duration/distance must keep this well below auto-merge.
        var race = Candidate(SportType.Running, BaseStart, 45 * 60, 10000);
        var warmup = Incoming(SportType.Running, BaseStart.AddMinutes(-15), 12 * 60, 2000);

        var (score, _) = ActivityMatchingService.Score(race, null, warmup, Options);

        score.Should().BeLessThan(Options.AutoMergeThreshold);
    }

    [Fact]
    public void ManuallySplitRun_TwoHalves_DoNotFalsePositiveMerge()
    {
        // A single 60-minute/10km run split into two ~30-minute/5km halves — each half differs
        // from the whole by ~50% on both duration and distance, well past the decay window, so
        // even sharing a start time must never be enough to auto-merge them as the same activity.
        var wholeRun = Candidate(SportType.Running, BaseStart, 3600, 10000);
        var firstHalf = Incoming(SportType.Running, BaseStart, 1800, 5000);

        var (score, _) = ActivityMatchingService.Score(wholeRun, null, firstHalf, Options);

        score.Should().BeLessThan(Options.AutoMergeThreshold);
    }

    [Fact]
    public void GarminActivityViaStravaAndViaIntervalsIcu_SameFingerprint_AutoMerges()
    {
        // The same real Garmin-recorded run, synced once via Strava and once via intervals.icu —
        // tiny rounding/reporting differences between the two APIs, not a real difference.
        var viaStrava = Candidate(SportType.Running, BaseStart, 3602, 10015);
        var viaIntervalsIcu = Incoming(SportType.Running, BaseStart.AddSeconds(3), 3600, 10008, deviceName: "Garmin Forerunner 965");

        var (score, _) = ActivityMatchingService.Score(viaStrava, "Garmin Forerunner 965", viaIntervalsIcu, Options);

        score.Should().BeGreaterThanOrEqualTo(Options.AutoMergeThreshold);
    }

    [Fact]
    public void RenamedActivity_TitleDiffers_StillMatchesOnOtherSignals()
    {
        var candidate = Candidate(SportType.Running, BaseStart, 3600, 10000);
        var incoming = Incoming(SportType.Running, BaseStart, 3600, 10000, title: "Completely different title");

        var (score, _) = ActivityMatchingService.Score(candidate, null, incoming, Options);

        score.Should().BeGreaterThanOrEqualTo(Options.AutoMergeThreshold);
    }

    [Fact]
    public void NoGpsActivity_NoDistance_CappedBelowAutoMergeThreshold_UnlessDeviceMatches()
    {
        // 3 minutes apart: outside the exact-match band (see the next tests), so the cap applies.
        var candidate = Candidate(SportType.Running, BaseStart, 3600, null);
        var incoming = Incoming(SportType.Running, BaseStart.AddMinutes(3), 3600, null);

        var (withoutDevice, _) = ActivityMatchingService.Score(candidate, null, incoming, Options);
        withoutDevice.Should().BeLessThan(Options.AutoMergeThreshold);
        withoutDevice.Should().BeLessThanOrEqualTo(Options.NoDistanceActivityMaxScore);

        var incomingWithDevice = Incoming(SportType.Running, BaseStart.AddMinutes(3), 3600, null, deviceName: "Wahoo Elemnt");
        var (withDevice, _) = ActivityMatchingService.Score(candidate, "Wahoo Elemnt", incomingWithDevice, Options);
        withDevice.Should().BeGreaterThan(withoutDevice);
    }

    [Fact]
    public void NoDistance_SameStartAndDuration_AutoMerges()
    {
        // Real case: a strength session reported by Strava (distance 0) and intervals.icu (no
        // distance) with identical start second and duration.
        var viaIntervalsIcu = Candidate(SportType.Strength, BaseStart, 1080, null);
        var viaStrava = Incoming(SportType.Strength, BaseStart.AddSeconds(30), 1090, 0);

        var (score, breakdown) = ActivityMatchingService.Score(viaIntervalsIcu, null, viaStrava, Options);

        score.Should().BeGreaterThanOrEqualTo(Options.AutoMergeThreshold);
        breakdown["noDistanceExactMatch"].Should().Be(1);
    }

    [Fact]
    public void NoDistance_SameStart_ProvidersDisagreeOnMovingTime_AutoMerges()
    {
        // Real case: a workout started on the same second, 1 590 s via intervals.icu, 1 645 s via Strava (3.5 %).
        var viaIntervalsIcu = Candidate(SportType.Strength, BaseStart, 1590, null);
        var viaStrava = Incoming(SportType.Strength, BaseStart, 1645, 0);

        var (score, _) = ActivityMatchingService.Score(viaIntervalsIcu, null, viaStrava, Options);

        score.Should().BeGreaterThanOrEqualTo(Options.AutoMergeThreshold);
    }

    [Theory]
    [InlineData(2, 1080)] // two minutes apart
    [InlineData(0, 1250)] // same start, ~15 % longer
    public void NoDistance_OutsideExactBand_StaysCapped(int minutesApart, int incomingDuration)
    {
        var candidate = Candidate(SportType.Strength, BaseStart, 1080, null);
        var incoming = Incoming(SportType.Strength, BaseStart.AddMinutes(minutesApart), incomingDuration, null);

        var (score, _) = ActivityMatchingService.Score(candidate, null, incoming, Options);

        score.Should().BeLessThanOrEqualTo(Options.NoDistanceActivityMaxScore);
    }

    [Fact]
    public void StrengthTraining_NoDistance_ScoresOnSportTimeDeviceOnly()
    {
        var candidate = Candidate(SportType.Strength, BaseStart, 2700, null);
        var incoming = Incoming(SportType.Strength, BaseStart.AddMinutes(1), 2700, null);

        var (score, breakdown) = ActivityMatchingService.Score(candidate, null, incoming, Options);

        breakdown["hasDistanceBoth"].Should().Be(0);
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DeviceNameMatch_AddsBonus_ButNeverSoleDeterminant()
    {
        // Far apart in time and with very different duration/distance (well beyond every decay
        // window) — sport + a matching device alone must not be enough to reach even the
        // manual-review band.
        var candidate = Candidate(SportType.Running, BaseStart, 3600, 10000);
        var incoming = Incoming(SportType.Running, BaseStart.AddDays(3), 1000, 2000, deviceName: "Garmin Forerunner 965");

        var (score, _) = ActivityMatchingService.Score(candidate, "Garmin Forerunner 965", incoming, Options);

        score.Should().BeLessThan(Options.ManualReviewThreshold);
    }
}
