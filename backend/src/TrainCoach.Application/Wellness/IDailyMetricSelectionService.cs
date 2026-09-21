using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public record DailyMetricSelectionDto(DateOnly Date, WellnessMetricKind MetricKind, DataSource SelectedSource, decimal? SelectedValue, string PrecedenceRuleApplied);

/// <summary>
/// Resolves which source "wins" for one athlete/day/metric across the never-averaged, one-row-
/// per-source-per-day wellness tables (HrvMeasurement, RecoveryMetric, SleepRecord,
/// WeightMeasurement, TrainingLoadSnapshot). Purely a read-time resolution layer — recomputing a
/// selection never deletes or changes the underlying per-source rows. See
/// docs/integrations/canonical-data-and-deduplication-plan.md.
/// </summary>
public interface IDailyMetricSelectionService
{
    /// <summary>Recomputes and upserts the DailyMetricSelection row for every metric kind that has
    /// at least one source observation for this athlete/day. Called once per athlete+date at the
    /// end of a wellness sync (not per-field) to avoid redundant recomputation.</summary>
    Task RecomputeForAthleteDayAsync(Guid athleteUserId, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyMetricSelectionDto>> GetForAthleteDateRangeAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
