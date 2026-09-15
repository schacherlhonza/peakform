using Microsoft.EntityFrameworkCore;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;

namespace TrainCoach.Application.Common;

public class RelationshipAccessGuard(IApplicationDbContext db, ICurrentUserService currentUser) : IRelationshipAccessGuard
{
    public async Task EnsureAthleteAccessAsync(Guid targetAthleteUserId, PermissionScope? requiredScope, CancellationToken cancellationToken = default)
    {
        if (!await HasAthleteAccessAsync(targetAthleteUserId, requiredScope, cancellationToken))
        {
            throw new ForbiddenAccessException(
                "Nemáte oprávnění k datům tohoto sportovce.");
        }
    }

    public async Task<bool> HasAthleteAccessAsync(Guid targetAthleteUserId, PermissionScope? requiredScope, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        // The athlete always has full access to their own data.
        if (currentUser.Role == AppRole.Athlete && currentUser.UserId == targetAthleteUserId)
        {
            return true;
        }

        if (currentUser.Role != AppRole.Coach)
        {
            return false;
        }

        IQueryable<CoachAthleteRelationship> query = db.CoachAthleteRelationships
            .Where(r => r.CoachUserId == currentUser.UserId
                        && r.AthleteUserId == targetAthleteUserId
                        && r.Status == RelationshipStatus.Active);

        if (requiredScope is null)
        {
            return await query.AnyAsync(cancellationToken);
        }

        return await query
            .SelectMany(r => r.Permissions)
            .AnyAsync(p => p.Scope == requiredScope && p.RevokedAtUtc == null, cancellationToken);
    }
}
