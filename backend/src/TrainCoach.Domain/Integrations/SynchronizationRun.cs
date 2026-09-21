using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Integrations;

public class SynchronizationRun : Entity
{
    public Guid IntegrationConnectionId { get; set; }
    public IntegrationConnection IntegrationConnection { get; set; } = null!;

    public SyncTrigger Trigger { get; set; }
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Pending;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    public int ItemsFetched { get; set; }
    public int ItemsCreated { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsSkippedDuplicate { get; set; }

    /// <summary>Incremented when the matcher found a candidate in the 60-84 confidence band and
    /// queued a <see cref="TrainCoach.Domain.Execution.DuplicateCandidate"/> for review, rather
    /// than auto-merging or creating a fully separate activity.</summary>
    public int ItemsFlaggedForReview { get; set; }
    public string? ErrorMessage { get; set; }
}
