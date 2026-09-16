using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Common;

/// <summary>
/// Records sensitive operations into the append-only <see cref="TrainCoach.Domain.Identity.AuditLog"/>
/// table (see docs/security.md §12) — access grants/revocations, logins, exports, deletions. This is
/// for after-the-fact traceability (who/when/what), not routine operational logging.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Stages an audit entry on the current <see cref="IApplicationDbContext"/> unit of work. Does not
    /// call SaveChangesAsync itself — it persists together with the caller's own save, so the audit
    /// entry never survives a rolled-back operation and never costs an extra round trip.
    /// </summary>
    void Record(Guid? actorUserId, AuditAction action, string entityName, Guid? entityId = null, string? details = null, string? ipAddress = null);
}
