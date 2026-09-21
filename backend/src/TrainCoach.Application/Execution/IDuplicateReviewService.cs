namespace TrainCoach.Application.Execution;

/// <summary>
/// Athlete self-service review of the matcher's 60-84 confidence "probably the same" band — same
/// trust model as IIntegrationConnectionService: only the athlete who owns the activities reviews
/// and decides on their own duplicate candidates. See
/// docs/integrations/canonical-data-and-deduplication-plan.md.
/// </summary>
public interface IDuplicateReviewService
{
    Task<IReadOnlyList<DuplicateCandidateDto>> GetPendingAsync(Guid callerUserId, Guid athleteUserId, CancellationToken cancellationToken = default);

    /// <summary>Confirms two candidates are the same real-world activity: the losing activity's
    /// source records are re-pointed onto the surviving one and it is soft-deleted — never hard
    /// deleted, and always revertible via <see cref="RevertAsync"/>.</summary>
    Task MergeAsync(Guid callerUserId, Guid duplicateCandidateId, Guid survivingActivityId, CancellationToken cancellationToken = default);

    /// <summary>Confirms the two candidates are genuinely different activities.</summary>
    Task DismissAsync(Guid callerUserId, Guid duplicateCandidateId, CancellationToken cancellationToken = default);

    Task RevertAsync(Guid callerUserId, Guid mergeDecisionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MergeDecisionDto>> GetMergeDecisionsAsync(Guid callerUserId, Guid athleteUserId, CancellationToken cancellationToken = default);
}
