using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Integrations.Matching;

public enum ActivityMatchOutcome
{
    /// <summary>No existing canonical activity could plausibly be the same real-world event.</summary>
    NoCandidate,

    /// <summary>Score is at/above <see cref="ActivityMatchingOptions.AutoMergeThreshold"/> — attach
    /// instead of creating a new canonical activity.</summary>
    AutoMerge,

    /// <summary>Score is in the manual-review band — create as normal but flag for human review.</summary>
    FlagForReview,

    /// <summary>A candidate existed but scored below the review threshold — treat as a genuinely
    /// separate activity.</summary>
    TreatAsSeparate,
}

public record ActivityMatchResult(
    ActivityMatchOutcome Outcome,
    Guid? MatchedActivityId,
    int? ConfidenceScore,
    string? ScoringBreakdownJson,
    MergeDecisionKind? DecisionKind);

/// <summary>
/// The matching cascade beyond level-1 idempotent upsert (which stays in ActivityIngestionService, on the
/// existing (Source, ExternalId) unique index — this service is only ever consulted after that
/// check misses). See docs/integrations/canonical-data-and-deduplication-plan.md /
/// docs/integrations/activity-matching.md for the full level 2-5 algorithm.
/// </summary>
public interface IActivityMatchingService
{
    Task<ActivityMatchResult> FindOrScoreMatchAsync(
        Guid athleteUserId,
        ExternalActivity incoming,
        DataSource incomingSource,
        CancellationToken cancellationToken = default);
}
