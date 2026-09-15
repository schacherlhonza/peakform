using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    AppRole Role,
    string? TimeZoneId,
    string? Locale);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record AuthResult(
    Guid UserId,
    string Email,
    string DisplayName,
    AppRole Role,
    bool IsOnboarded,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public class AuthenticationFailedException(string message) : Exception(message);
