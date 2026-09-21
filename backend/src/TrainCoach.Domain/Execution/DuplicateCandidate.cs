using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// A pending work-queue item: two <see cref="CompletedActivity"/> records the matcher scored in
/// the 60-84 "probably the same" band, awaiting the athlete's confirmation. Distinct from
/// <see cref="MergeDecision"/>, which is the permanent record of what was ultimately decided
/// (including fully-automatic decisions that never went through this queue at all).
/// </summary>
public class DuplicateCandidate : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public Guid ActivityAId { get; set; }
    public Guid ActivityBId { get; set; }
    public int ConfidenceScore { get; set; }
    public string ScoringBreakdownJson { get; set; } = "{}";
    public DuplicateCandidateStatus Status { get; set; } = DuplicateCandidateStatus.Pending;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public enum DuplicateCandidateStatus
{
    Pending = 1,
    MergedIntoA = 2,
    MergedIntoB = 3,
    DismissedAsDistinct = 4,
}
