using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Common;

public record UserLookupResult(Guid UserId, string Email, AppRole Role);

/// <summary>The narrow seam Application uses to reach Identity (email lookup) without depending
/// on ApplicationUser/UserManager directly. Implemented in Infrastructure.</summary>
public interface IUserLookupService
{
    Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, string>> GetEmailsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
}
