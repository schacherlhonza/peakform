using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public record IntegrationConnectionDto(
    Guid Id,
    Guid AthleteUserId,
    IntegrationProviderType Provider,
    IntegrationConnectionStatus Status,
    string? ExternalAccountId,
    DateTime? ConnectedAtUtc,
    DateTime? LastSyncedAtUtc,
    DateTime? HeartRateZonesSyncedAtUtc = null,
    string? HeartRateZonesSyncError = null,
    bool PushPlannedWorkouts = false);

public record AuthorizationUrlDto(string Url, string State);

public record SynchronizationRunDto(
    Guid Id,
    IntegrationProviderType Provider,
    SyncRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int ItemsFetched,
    int ItemsCreated,
    int ItemsSkippedDuplicate,
    string? ErrorMessage,
    SyncTrigger Trigger,
    int ItemsUpdated,
    int ItemsFlaggedForReview);

/// <summary>One row of the app-wide sync indicator: an active (or errored) connection plus its most
/// recent run — a <see cref="SyncRunStatus.Pending"/> run means queued but not yet picked up.</summary>
public record ProviderSyncStatusDto(
    IntegrationProviderType Provider,
    IntegrationConnectionStatus ConnectionStatus,
    DateTime? LastSyncedAtUtc,
    SynchronizationRunDto? LatestRun);
