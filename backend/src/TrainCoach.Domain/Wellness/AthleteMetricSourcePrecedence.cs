using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// An athlete's own override of which source should win for one metric kind when several sources
/// report a value for the same day. Lower <see cref="Rank"/> wins. When an athlete has no rows for
/// a given <see cref="MetricKind"/>, IDailyMetricSelectionService falls back to a hardcoded default
/// precedence in code (replacing the old, never-consumed global <c>SourcePrecedence</c> enum).
/// </summary>
public class AthleteMetricSourcePrecedence : Entity
{
    public Guid AthleteUserId { get; set; }
    public WellnessMetricKind MetricKind { get; set; }
    public DataSource Source { get; set; }
    public int Rank { get; set; }
}
