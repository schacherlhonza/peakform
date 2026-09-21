namespace TrainCoach.Application.Integrations.Matching;

public record DuplicateDryRunReportDto(
    Guid Id,
    Guid? AthleteUserId,
    DateTime GeneratedAtUtc,
    int TotalActivitiesScanned,
    int ExactTierCount,
    int HighConfidenceTierCount,
    int UncertainTierCount,
    string CandidatesJson);

/// <summary>
/// Runs the matcher read-only over existing <c>CompletedActivity</c> rows and reports counts by
/// confidence tier — never writes MergeDecision/DuplicateCandidate rows itself. Intended to be
/// reviewed by a human before any auto-merge migration step runs. See
/// docs/integrations/canonical-data-and-deduplication-plan.md.
/// </summary>
public interface IDuplicateDryRunReportService
{
    Task<DuplicateDryRunReportDto> GenerateAsync(Guid? athleteUserId, CancellationToken cancellationToken = default);

    Task<DuplicateDryRunReportDto?> GetLatestAsync(CancellationToken cancellationToken = default);
}
