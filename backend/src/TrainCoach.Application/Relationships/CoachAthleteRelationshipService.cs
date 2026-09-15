using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;

namespace TrainCoach.Application.Relationships;

public class CoachAthleteRelationshipService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    IDateTimeProvider clock) : ICoachAthleteRelationshipService
{
    // Granted automatically when an athlete accepts an invite, so the core flow (coach builds a
    // plan, sees activity/wellness, comments) works immediately. The athlete can revoke any of
    // these afterwards from the permissions screen.
    private static readonly PermissionScope[] DefaultScopesOnAccept =
    [
        PermissionScope.ViewTrainingPlan,
        PermissionScope.EditTrainingPlan,
        PermissionScope.ViewCompletedActivities,
        PermissionScope.ViewWellness,
        PermissionScope.CommentOnWorkouts,
    ];

    public async Task<CoachAthleteRelationshipDto> InviteAthleteAsync(Guid coachUserId, InviteAthleteRequest request, CancellationToken cancellationToken = default)
    {
        var athlete = await userLookup.FindByEmailAsync(request.AthleteEmail, cancellationToken)
            ?? throw new NotFoundException("Sportovec", request.AthleteEmail);

        if (athlete.Role != AppRole.Athlete)
        {
            throw new BusinessRuleException("Zadaný e-mail nepatří účtu sportovce.");
        }

        var alreadyPending = await db.CoachAthleteRelationships.AnyAsync(
            r => r.CoachUserId == coachUserId && r.AthleteUserId == athlete.UserId
                 && (r.Status == RelationshipStatus.PendingInvite || r.Status == RelationshipStatus.Active),
            cancellationToken);
        if (alreadyPending)
        {
            throw new BusinessRuleException("S tímto sportovcem už existuje aktivní nebo čekající spolupráce.");
        }

        var relationship = new CoachAthleteRelationship
        {
            CoachUserId = coachUserId,
            AthleteUserId = athlete.UserId,
            Status = RelationshipStatus.PendingInvite,
            InvitedByUserId = coachUserId,
            InvitedAtUtc = clock.UtcNow,
            InviteNote = request.Note,
            CreatedAtUtc = clock.UtcNow,
        };
        db.CoachAthleteRelationships.Add(relationship);
        await db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(relationship, cancellationToken);
    }

    public async Task<CoachAthleteRelationshipDto> RespondToInviteAsync(Guid athleteUserId, Guid relationshipId, RespondToInviteRequest request, CancellationToken cancellationToken = default)
    {
        var relationship = await LoadOwnedAsync(relationshipId, cancellationToken);
        if (relationship.AthleteUserId != athleteUserId)
        {
            throw new ForbiddenAccessException("Toto pozvání nepatří vám.");
        }

        if (relationship.Status != RelationshipStatus.PendingInvite)
        {
            throw new BusinessRuleException("Toto pozvání již bylo vyřízeno.");
        }

        relationship.RespondedAtUtc = clock.UtcNow;

        if (request.Accept)
        {
            relationship.Status = RelationshipStatus.Active;
            relationship.StartDateUtc = clock.UtcNow;

            foreach (var scope in DefaultScopesOnAccept)
            {
                db.RelationshipPermissions.Add(new RelationshipPermission
                {
                    RelationshipId = relationship.Id,
                    Scope = scope,
                    GrantedByUserId = athleteUserId,
                    GrantedAtUtc = clock.UtcNow,
                });
            }
        }
        else
        {
            relationship.Status = RelationshipStatus.Declined;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(relationship, cancellationToken);
    }

    public async Task<CoachAthleteRelationshipDto> RevokeAsync(Guid actingUserId, Guid relationshipId, RevokeAccessRequest request, CancellationToken cancellationToken = default)
    {
        var relationship = await LoadOwnedAsync(relationshipId, cancellationToken);
        if (relationship.AthleteUserId != actingUserId && relationship.CoachUserId != actingUserId)
        {
            throw new ForbiddenAccessException("Tato spolupráce se vás netýká.");
        }

        if (relationship.Status != RelationshipStatus.Active && relationship.Status != RelationshipStatus.PendingInvite)
        {
            throw new BusinessRuleException("Tato spolupráce už není aktivní.");
        }

        relationship.Status = RelationshipStatus.Revoked;
        relationship.RevokedAtUtc = clock.UtcNow;
        relationship.RevokedByUserId = actingUserId;
        relationship.RevokedReason = request.Reason;
        relationship.EndDateUtc = clock.UtcNow;

        var activePermissions = await db.RelationshipPermissions
            .Where(p => p.RelationshipId == relationship.Id && p.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var permission in activePermissions)
        {
            permission.RevokedAtUtc = clock.UtcNow;
            permission.RevokedByUserId = actingUserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(relationship, cancellationToken);
    }

    public async Task<CoachAthleteRelationshipDto> SetPermissionAsync(Guid athleteUserId, Guid relationshipId, SetPermissionRequest request, CancellationToken cancellationToken = default)
    {
        var relationship = await LoadOwnedAsync(relationshipId, cancellationToken);
        if (relationship.AthleteUserId != athleteUserId)
        {
            throw new ForbiddenAccessException("Oprávnění této spolupráce může spravovat pouze sportovec.");
        }

        if (relationship.Status != RelationshipStatus.Active)
        {
            throw new BusinessRuleException("Oprávnění lze měnit pouze u aktivní spolupráce.");
        }

        var existing = await db.RelationshipPermissions.FirstOrDefaultAsync(
            p => p.RelationshipId == relationshipId && p.Scope == request.Scope && p.RevokedAtUtc == null,
            cancellationToken);

        if (request.Granted && existing is null)
        {
            db.RelationshipPermissions.Add(new RelationshipPermission
            {
                RelationshipId = relationshipId,
                Scope = request.Scope,
                GrantedByUserId = athleteUserId,
                GrantedAtUtc = clock.UtcNow,
            });
        }
        else if (!request.Granted && existing is not null)
        {
            existing.RevokedAtUtc = clock.UtcNow;
            existing.RevokedByUserId = athleteUserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(relationship, cancellationToken);
    }

    public async Task<IReadOnlyList<CoachAthleteRelationshipDto>> GetMyRelationshipsAsync(Guid currentUserId, AppRole currentUserRole, CancellationToken cancellationToken = default)
    {
        var query = currentUserRole == AppRole.Coach
            ? db.CoachAthleteRelationships.Where(r => r.CoachUserId == currentUserId)
            : db.CoachAthleteRelationships.Where(r => r.AthleteUserId == currentUserId);

        var relationships = await query
            .Include(r => r.Permissions)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await ToDtosAsync(relationships, cancellationToken);
    }

    private async Task<CoachAthleteRelationship> LoadOwnedAsync(Guid relationshipId, CancellationToken cancellationToken)
    {
        return await db.CoachAthleteRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == relationshipId, cancellationToken)
            ?? throw new NotFoundException("CoachAthleteRelationship", relationshipId);
    }

    private async Task<CoachAthleteRelationshipDto> ToDtoAsync(CoachAthleteRelationship relationship, CancellationToken cancellationToken)
        => (await ToDtosAsync([relationship], cancellationToken))[0];

    private async Task<IReadOnlyList<CoachAthleteRelationshipDto>> ToDtosAsync(IReadOnlyList<CoachAthleteRelationship> relationships, CancellationToken cancellationToken)
    {
        if (relationships.Count == 0)
        {
            return [];
        }

        var userIds = relationships.SelectMany(r => new[] { r.CoachUserId, r.AthleteUserId }).Distinct().ToList();
        var profiles = await db.UserProfiles.Where(p => userIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var emails = await userLookup.GetEmailsAsync(userIds, cancellationToken);

        return relationships.Select(r => new CoachAthleteRelationshipDto(
            r.Id,
            r.CoachUserId,
            profiles.TryGetValue(r.CoachUserId, out var coach) ? coach.DisplayName : "?",
            emails.GetValueOrDefault(r.CoachUserId, string.Empty),
            r.AthleteUserId,
            profiles.TryGetValue(r.AthleteUserId, out var athlete) ? athlete.DisplayName : "?",
            emails.GetValueOrDefault(r.AthleteUserId, string.Empty),
            r.Status,
            r.InvitedAtUtc,
            r.RespondedAtUtc,
            r.StartDateUtc,
            r.EndDateUtc,
            r.InviteNote,
            r.Permissions.Where(p => p.RevokedAtUtc == null).Select(p => p.Scope).ToList())).ToList();
    }
}
