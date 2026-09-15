using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// A single metric observation for an activity, keyed by type + source so the same metric can
/// be recorded from multiple providers without overwriting one another (e.g. distance from
/// Strava and a separately-imported HR trace). The value chosen as authoritative on
/// <see cref="CompletedActivity"/>'s fixed columns is resolved by <see cref="SourcePrecedence"/>.
/// </summary>
public class ActivityMetric : Entity
{
    public Guid CompletedActivityId { get; set; }
    public CompletedActivity CompletedActivity { get; set; } = null!;

    public ActivityMetricType MetricType { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DataSource Source { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
