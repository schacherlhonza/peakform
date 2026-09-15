using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Identity;

/// <summary>
/// A single granted scope on a <see cref="CoachAthleteRelationship"/>. Modeled as a
/// claims-style row-per-scope table (rather than a bitmask column) so new scopes can be
/// introduced without a migration, and so each grant/revoke carries its own audit trail.
/// </summary>
public class RelationshipPermission : Entity
{
    public Guid RelationshipId { get; set; }
    public CoachAthleteRelationship Relationship { get; set; } = null!;

    public PermissionScope Scope { get; set; }

    public Guid GrantedByUserId { get; set; }
    public DateTime GrantedAtUtc { get; set; }

    public Guid? RevokedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc is null;
}
