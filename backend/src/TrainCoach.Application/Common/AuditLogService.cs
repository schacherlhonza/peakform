using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;

namespace TrainCoach.Application.Common;

public class AuditLogService(IApplicationDbContext db, IDateTimeProvider clock) : IAuditLogService
{
    public void Record(Guid? actorUserId, AuditAction action, string entityName, Guid? entityId = null, string? details = null, string? ipAddress = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            OccurredAtUtc = clock.UtcNow,
        });
    }
}
