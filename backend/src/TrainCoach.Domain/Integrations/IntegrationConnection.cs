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

    /// <summary>Last successful write of PeakForm's heart rate zones to the provider (see ITrainingSettingsSyncProvider).</summary>
    public DateTime? HeartRateZonesSyncedAtUtc { get; set; }

    /// <summary>Why the last zone write failed (user-facing); null after a success.</summary>
    public string? HeartRateZonesSyncError { get; set; }

    /// <summary>
    /// The athlete's consent to put coach-planned workouts on this provider's calendar (and from there
    /// on their watch). Off by default — set by the athlete only.
    /// </summary>
    public bool PushPlannedWorkouts { get; set; }

    public IntegrationCredential? Credential { get; set; }
    public ICollection<SynchronizationRun> SyncRuns { get; set; } = new List<SynchronizationRun>();
}
