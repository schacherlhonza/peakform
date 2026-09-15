using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Identity;

/// <summary>
/// The relationship between a coach and an athlete. All cross-user authorization in the
/// application is ultimately resolved through an *active* relationship plus its granted
/// <see cref="RelationshipPermission"/> scopes — a coach must never reach an athlete's data
/// by ID alone. An athlete may have relationships with more than one coach over time (only
/// one of which needs to be Active at once, though the model does not forbid several).
/// </summary>
public class CoachAthleteRelationship : AuditableEntity
{
    public Guid CoachUserId { get; set; }
    public Guid AthleteUserId { get; set; }

    public RelationshipStatus Status { get; set; } = RelationshipStatus.PendingInvite;

    public Guid InvitedByUserId { get; set; }
    public DateTime InvitedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }

    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }

    public Guid? RevokedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }

    public string? InviteNote { get; set; }

    public ICollection<RelationshipPermission> Permissions { get; set; } = new List<RelationshipPermission>();
}
