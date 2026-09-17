namespace TrainCoach.Application.Account;

/// <summary>
/// Self-service "right to erasure" (GDPR, security.md §11). Implemented in the Infrastructure
/// layer because it must anonymize the ASP.NET Core Identity record (email/username/lockout),
/// which the Application layer never references directly.
/// </summary>
public interface IAccountDeletionService
{
    Task DeleteMyAccountAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default);
}
