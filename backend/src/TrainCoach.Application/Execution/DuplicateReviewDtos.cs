using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

public record DuplicateCandidateDto(
    Guid Id,
    Guid AthleteUserId,
    CompletedActivityDto ActivityA,
    CompletedActivityDto ActivityB,
    int ConfidenceScore,
    string ScoringBreakdownJson,
    DuplicateCandidateStatus Status);

public record MergeDecisionDto(
    Guid Id,
    Guid AthleteUserId,
    Guid SurvivingActivityId,
    Guid AbsorbedSourceRecordId,
    int ConfidenceScore,
    MergeDecisionKind Kind,
    MergeDecisionOutcome Outcome,
    DateTime DecidedAtUtc,
    DateTime? RevertedAtUtc);

public record MergeDuplicateCandidateRequest(Guid SurvivingActivityId);
