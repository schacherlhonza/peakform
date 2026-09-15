using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Relationships;

public record InviteAthleteRequest(string AthleteEmail, string? Note);

public record RespondToInviteRequest(bool Accept);

public record RevokeAccessRequest(string? Reason);

public record SetPermissionRequest(PermissionScope Scope, bool Granted);

public record CoachAthleteRelationshipDto(
    Guid Id,
    Guid CoachUserId,
    string CoachName,
    string CoachEmail,
    Guid AthleteUserId,
    string AthleteName,
    string AthleteEmail,
    RelationshipStatus Status,
    DateTime InvitedAtUtc,
    DateTime? RespondedAtUtc,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    string? InviteNote,
    IReadOnlyList<PermissionScope> GrantedScopes);
