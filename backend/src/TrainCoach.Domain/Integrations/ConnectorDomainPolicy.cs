using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Integrations;

/// <summary>
/// How much authority one provider has for one data domain, for one athlete — e.g. Strava
/// Activities=FallbackOnly once intervals.icu is connected, so Strava stops creating a second
/// canonical activity for something intervals.icu already reported. Always athlete-scoped (there
/// is no global policy row) because the right default inherently depends on that athlete's own
/// connection state. IsAthleteOverride=false rows are recomputed by
/// IConnectorPolicyService.EnsureDefaultsAsync whenever connection state changes; override rows
/// are never touched automatically. See docs/integrations/canonical-data-and-deduplication-plan.md.
/// </summary>
public class ConnectorDomainPolicy : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public IntegrationProviderType Provider { get; set; }
    public DataDomain Domain { get; set; }
    public ConnectorMode Mode { get; set; }
    public bool IsAthleteOverride { get; set; }
}
