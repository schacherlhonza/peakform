using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Relationships;

public interface ICoachAthleteRelationshipService
{
    /// <summary>Coach invites an athlete by email. The athlete must already have an account
    /// with the Athlete role (MVP: no email invites to not-yet-registered people).</summary>
    Task<CoachAthleteRelationshipDto> InviteAthleteAsync(Guid coachUserId, InviteAthleteRequest request, CancellationToken cancellationToken = default);

    Task<CoachAthleteRelationshipDto> RespondToInviteAsync(Guid athleteUserId, Guid relationshipId, RespondToInviteRequest request, CancellationToken cancellationToken = default);

    Task<CoachAthleteRelationshipDto> RevokeAsync(Guid actingUserId, Guid relationshipId, RevokeAccessRequest request, CancellationToken cancellationToken = default);

    Task<CoachAthleteRelationshipDto> SetPermissionAsync(Guid athleteUserId, Guid relationshipId, SetPermissionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every relationship for the caller (coach: their athletes; athlete: their coaches), any status.</summary>
    Task<IReadOnlyList<CoachAthleteRelationshipDto>> GetMyRelationshipsAsync(Guid currentUserId, AppRole currentUserRole, CancellationToken cancellationToken = default);
}
