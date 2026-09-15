using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Identity;

/// <summary>Append-only record of sensitive operations (access grants/revocations, logins, exports, deletions...).</summary>
public class AuditLog : Entity
{
    public Guid? ActorUserId { get; set; }
    public AuditAction Action { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
