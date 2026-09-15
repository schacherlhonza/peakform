using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Integrations;

public class IntegrationConnection : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public IntegrationProviderType Provider { get; set; }
    public IntegrationConnectionStatus Status { get; set; } = IntegrationConnectionStatus.NotConnected;
    public string? ExternalAccountId { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? DisconnectedAtUtc { get; set; }

    public IntegrationCredential? Credential { get; set; }
    public ICollection<SynchronizationRun> SyncRuns { get; set; } = new List<SynchronizationRun>();
}
