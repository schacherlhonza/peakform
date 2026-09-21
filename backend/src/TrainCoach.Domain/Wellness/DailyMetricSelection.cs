using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// Cache/audit of which source "wins" for one athlete/day/metric, computed by
/// IDailyMetricSelectionService from the (never-averaged, one-row-per-source-per-day) wellness
/// tables — <see cref="HrvMeasurement"/>, <see cref="RecoveryMetric"/>, <see cref="SleepRecord"/>,
/// <see cref="WeightMeasurement"/>, <see cref="TrainingLoadSnapshot"/>. This table is a read-time
/// resolution result, not a source of truth: recomputing it never deletes or changes the
/// underlying per-source rows.
/// </summary>
public class DailyMetricSelection : Entity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public WellnessMetricKind MetricKind { get; set; }
    public DataSource SelectedSource { get; set; }

    /// <summary>Id of the row in whichever of the 5 per-source tables was picked. Not a DB foreign
    /// key — which table it points into is determined by <see cref="MetricKind"/>.</summary>
    public Guid SelectedRecordId { get; set; }

    /// <summary>Denormalized copy of the selected value, for fast dashboard reads without a join.</summary>
    public decimal? SelectedValue { get; set; }

    /// <summary>E.g. "AthleteOverride:IntervalsIcu" or "DefaultPrecedence:IntervalsIcu&gt;Strava".</summary>
    public string PrecedenceRuleApplied { get; set; } = string.Empty;

    public DateTime ComputedAtUtc { get; set; }
}
