namespace TrainCoach.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);
    Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
