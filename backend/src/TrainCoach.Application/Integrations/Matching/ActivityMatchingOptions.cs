namespace TrainCoach.Application.Integrations.Matching;

/// <summary>
/// Tuning knobs for IActivityMatchingService's level-5 confidence scoring. An engineering-wide
/// setting (not per-athlete — see Open Question 4 in
/// docs/integrations/canonical-data-and-deduplication-plan.md), bound from configuration the same
/// way <c>StravaOptions</c>/<c>IntervalsIcuOptions</c> are.
/// </summary>
public class ActivityMatchingOptions
{
    public const string SectionName = "Integrations:ActivityMatching";

    /// <summary>Score at or above which two candidates are auto-merged.</summary>
    public int AutoMergeThreshold { get; set; } = 85;

    /// <summary>Score at or above which (but below <see cref="AutoMergeThreshold"/>) a candidate is
    /// queued for manual review rather than treated as a separate activity.</summary>
    public int ManualReviewThreshold { get; set; } = 60;

    /// <summary>Minutes apart at/under which the time signal scores full marks, decaying linearly to 0 at <see cref="TimeWindowMinutesMax"/>.</summary>
    public int TimeWindowMinutesForFullScore { get; set; } = 5;

    /// <summary>Minutes apart beyond which the time signal contributes nothing.</summary>
    public int TimeWindowMinutesMax { get; set; } = 120;

    /// <summary>Distance difference (as a fraction of the larger value) at/under which the distance signal scores full marks.</summary>
    public decimal DistanceTolerancePercentForFullScore { get; set; } = 0.02m;

    /// <summary>Distance difference beyond which the distance signal contributes nothing.</summary>
    public decimal DistanceToleranceMaxPercent { get; set; } = 0.25m;

    /// <summary>Duration difference (as a fraction of the larger value) at/under which the duration signal scores full marks.</summary>
    public decimal DurationTolerancePercentForFullScore { get; set; } = 0.05m;

    /// <summary>Duration difference beyond which the duration signal contributes nothing.</summary>
    public decimal DurationToleranceMaxPercent { get; set; } = 0.30m;

    /// <summary>Bonus added (never subtracted) when both candidates report the same normalized device name.</summary>
    public int DeviceMatchBonus { get; set; } = 10;

    /// <summary>Hard ceiling on the total score when neither candidate has a distance (e.g. strength
    /// training, no-GPS activities) — prevents two different same-day no-distance sessions from
    /// false-positive auto-merging on sport+time alone.</summary>
    public int NoDistanceActivityMaxScore { get; set; } = 75;

    /// <summary>Exception to <see cref="NoDistanceActivityMaxScore"/>: no-distance activities whose
    /// start times are at most this many minutes apart <i>and</i> whose durations differ by at most
    /// <see cref="NoDistanceExactDurationTolerancePercent"/> aren't capped (so they auto-merge). Two
    /// genuinely different sessions practically never share both start and length — this is the
    /// same watch recording reported by two providers (e.g. a strength session via Strava and
    /// intervals.icu, which otherwise all landed in manual review).</summary>
    public int NoDistanceExactStartToleranceMinutes { get; set; } = 1;

    /// <summary>See <see cref="NoDistanceExactStartToleranceMinutes"/>.</summary>
    public decimal NoDistanceExactDurationTolerancePercent { get; set; } = 0.02m;
}
