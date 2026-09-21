using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// A stored snapshot of a duplicate-matching dry run over existing data — purely informational,
/// never writes MergeDecision/DuplicateCandidate rows by itself (see IDuplicateDryRunReportService).
/// Kept as its own small entity rather than overloading <c>GeneratedReport</c> (that entity's
/// shape — NarrativeText, DeliveryChannel/Status, one-per-athlete-per-day — belongs to the
/// wellness daily-report feature and doesn't fit an ops/admin artifact like this one).
/// </summary>
public class DuplicateDryRunReport : Entity
{
    /// <summary>Null when the run covered every athlete.</summary>
    public Guid? AthleteUserId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
    public int ExactTierCount { get; set; }
    public int HighConfidenceTierCount { get; set; }
    public int UncertainTierCount { get; set; }
    public int TotalActivitiesScanned { get; set; }

    /// <summary>JSON array of candidate pairs with their score/breakdown — see docs/integrations/activity-matching.md.</summary>
    public string CandidatesJson { get; set; } = "[]";
}
