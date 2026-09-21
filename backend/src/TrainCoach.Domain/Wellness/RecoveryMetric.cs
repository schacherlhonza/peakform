using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// Daily recovery snapshot per source — grew beyond a strict "recovery" name to also cover other
/// per-day scored/derived wellness signals from provider sync (mood/soreness/fatigue/motivation,
/// steps, SpO2, VO2max, an injury flag) rather than proliferating a separate single-column table
/// for each. Kept as one entity deliberately: renaming it would ripple through the whole app for
/// a naming-purity gain only, which isn't worth the risk — see docs/integrations-research.md §5.
/// Mood/soreness/fatigue/motivation here are provider-synced values (per <see cref="Source"/>),
/// distinct from the athlete's own manual entry on <see cref="DailyCheckIn"/> — same "objective
/// synced vs. subjective manual" separation already used for <see cref="SleepRecord"/> vs.
/// <see cref="DailyCheckIn.SleepQuality"/>. Both can coexist for the same day without conflict.
/// </summary>
public class RecoveryMetric : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public int? RestingHeartRateBpm { get; set; }
    public int? ReadinessScore { get; set; }
    public int? StressScore { get; set; }
    public int? MoodScore { get; set; }
    public int? SorenessScore { get; set; }
    public int? FatigueScore { get; set; }
    public int? MotivationScore { get; set; }
    public int? Steps { get; set; }
    public decimal? SpO2Percent { get; set; }
    public decimal? Vo2Max { get; set; }

    /// <summary>Informational only — deliberately never used to auto-create/resolve a <see cref="PainOrHealthFlag"/>; that judgment is left to the athlete/coach.</summary>
    public bool? HasInjurySignal { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
}
