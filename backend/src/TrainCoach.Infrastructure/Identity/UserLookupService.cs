using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Identity;
using TrainCoach.Infrastructure.Persistence;

namespace TrainCoach.Infrastructure.Identity;

public class UserLookupService(UserManager<ApplicationUser> userManager, TrainCoachDbContext db) : IUserLookupService
{
    public async Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Id == user.Id, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        return new UserLookupResult(user.Id, user.Email!, profile.PrimaryRole);
    }

    public async Task<Dictionary<Guid, string>> GetEmailsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        return await db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email!, cancellationToken);
    }
}
