using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// Permanent, revertible audit trail of every merge (automatic or manual) between an incoming
/// <see cref="ActivitySourceRecord"/>/activity and an existing <see cref="CompletedActivity"/>.
/// Never deleted or mutated after the fact — a revert writes a new row, it never edits this one.
/// See docs/integrations/canonical-data-and-deduplication-plan.md §Deduplikace.
/// </summary>
public class MergeDecision : AuditableEntity
{
    public Guid AthleteUserId { get; set; }

    /// <summary>The <see cref="CompletedActivity"/> that remains canonical after this decision.</summary>
    public Guid SurvivingActivityId { get; set; }

    /// <summary>The <see cref="ActivitySourceRecord"/> that was attached to the surviving activity.</summary>
    public Guid AbsorbedSourceRecordId { get; set; }

    /// <summary>Set only when two already-canonical activities were merged via manual review
    /// (rather than a newly-synced source record being attached to an existing activity).</summary>
    public Guid? AbsorbedActivityIdBeforeMerge { get; set; }

    public int ConfidenceScore { get; set; }
    public MergeDecisionKind Kind { get; set; }
    public MergeDecisionOutcome Outcome { get; set; }

    /// <summary>Per-signal score breakdown (sport/time/distance/device) as JSON, for audit/debugging.</summary>
    public string? ScoringBreakdownJson { get; set; }

    /// <summary>Null for automatic decisions.</summary>
    public Guid? DecidedByUserId { get; set; }
    public DateTime DecidedAtUtc { get; set; }

    public DateTime? RevertedAtUtc { get; set; }
    public Guid? RevertedByUserId { get; set; }
}
