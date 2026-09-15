using Microsoft.AspNetCore.Identity;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Enum.GetNames<AppRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }
}
