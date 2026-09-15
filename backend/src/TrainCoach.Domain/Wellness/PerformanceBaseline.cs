using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// A rolling baseline (mean + spread) computed by a background job over a trailing window,
/// used by the report rule engine to flag deviations transparently instead of guessing.
/// </summary>
public class PerformanceBaseline : Entity
{
    public Guid AthleteUserId { get; set; }
    public BaselineMetricType MetricType { get; set; }
    public int WindowDays { get; set; }
    public decimal BaselineValue { get; set; }
    public decimal? StdDeviation { get; set; }
    public DateTime ComputedAtUtc { get; set; }
    public DateOnly ValidFromDate { get; set; }
}
