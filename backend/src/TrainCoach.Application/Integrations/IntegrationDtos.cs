using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public record IntegrationConnectionDto(
    Guid Id,
    Guid AthleteUserId,
    IntegrationProviderType Provider,
    IntegrationConnectionStatus Status,
    string? ExternalAccountId,
    DateTime? ConnectedAtUtc,
    DateTime? LastSyncedAtUtc);

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
    string? ErrorMessage);
