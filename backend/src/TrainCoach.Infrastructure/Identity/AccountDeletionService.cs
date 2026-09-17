using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Account;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;
using TrainCoach.Infrastructure.Persistence;

namespace TrainCoach.Infrastructure.Identity;

/// <summary>
/// Anonymizes rather than hard-deletes: several tables reference AthleteUserId/CoachUserId with
/// DeleteBehavior.Restrict specifically so a user's training/wellness history can't vanish out
/// from under the other party in a relationship (see IdentityConfigurations.cs). Erasure happens
/// by stripping personally-identifying fields and locking the account, exactly as
/// docs/security.md §11 describes ("soft delete... citlivé záznamy anonymizovány... agregovaná
/// historie... zachována bez osobní identifikace").
/// </summary>
public class AccountDeletionService(
    UserManager<ApplicationUser> userManager,
    TrainCoachDbContext db,
    IDateTimeProvider clock,
    IAuditLogService auditLog) : IAccountDeletionService
{
    public async Task DeleteMyAccountAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Id == userId, cancellationToken)
            ?? throw new NotFoundException("UserProfile", userId);

        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("ApplicationUser", userId);

        var activeRelationships = await db.CoachAthleteRelationships
            .Include(r => r.Permissions)
            .Where(r => (r.CoachUserId == userId || r.AthleteUserId == userId)
                        && (r.Status == RelationshipStatus.Active || r.Status == RelationshipStatus.PendingInvite))
            .ToListAsync(cancellationToken);

        foreach (var relationship in activeRelationships)
        {
            relationship.Status = RelationshipStatus.Revoked;
            relationship.RevokedAtUtc = clock.UtcNow;
            relationship.RevokedByUserId = userId;
            relationship.RevokedReason = "Účet byl smazán na žádost uživatele.";
            relationship.EndDateUtc = clock.UtcNow;

            foreach (var permission in relationship.Permissions.Where(p => p.RevokedAtUtc == null))
            {
                permission.RevokedAtUtc = clock.UtcNow;
                permission.RevokedByUserId = userId;
            }
        }

        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = clock.UtcNow;
            token.RevokedByIp = ipAddress;
        }

        auditLog.Record(userId, AuditAction.AccountDeleted, nameof(UserProfile), userId,
            $"Účet smazán, {activeRelationships.Count} spoluprací ukončeno.", ipAddress);

        // Anonymize domain-facing profile data. Numeric/health fields on AthleteProfile/CoachProfile
        // stay (they're only ever displayed alongside the now-anonymized name), but free-text fields
        // that could carry identifying content are cleared too.
        profile.FirstName = "Smazaný";
        profile.LastName = "účet";
        profile.PhoneNumber = null;
        profile.AvatarUrl = null;
        profile.IsDeleted = true;
        profile.DeletedAtUtc = clock.UtcNow;
        profile.DeletedByUserId = userId;

        var athleteProfile = await db.AthleteProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.UserProfileId == userId, cancellationToken);
        if (athleteProfile is not null)
        {
            athleteProfile.Notes = null;
        }

        var coachProfile = await db.CoachProfiles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.UserProfileId == userId, cancellationToken);
        if (coachProfile is not null)
        {
            coachProfile.Bio = null;
            coachProfile.Certifications = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Identity record: anonymize email/username so the address is freed up for re-registration,
        // and lock the account permanently so the (now anonymized) credentials can never sign in again.
        var anonymizedEmail = $"deleted-{userId:N}@peakform.invalid";
        await userManager.SetEmailAsync(user, anonymizedEmail);
        await userManager.SetUserNameAsync(user, anonymizedEmail);
        user.EmailConfirmed = false;
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await userManager.UpdateSecurityStampAsync(user);
        await userManager.UpdateAsync(user);
    }
}
