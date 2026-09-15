using Microsoft.AspNetCore.Identity;

namespace TrainCoach.Infrastructure.Identity;

/// <summary>
/// The ASP.NET Core Identity user record. Deliberately minimal — display/domain profile data
/// (name, role, timezone, locale...) lives in <see cref="TrainCoach.Domain.Identity.UserProfile"/>,
/// which shares this record's Id in a 1:1 relationship. This keeps auth/credential concerns
/// (Infrastructure) separate from domain-facing profile data (Domain).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
}
