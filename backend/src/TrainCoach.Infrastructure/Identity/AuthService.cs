using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Auth;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;
using TrainCoach.Infrastructure.Persistence;
using TrainCoach.Infrastructure.Security;

namespace TrainCoach.Infrastructure.Identity;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TrainCoachDbContext db,
    JwtTokenGenerator tokenGenerator,
    IDateTimeProvider clock) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new BusinessRuleException("Uživatel s tímto e-mailem už existuje.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw new BusinessRuleException(string.Join(" ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, request.Role.ToString());

        var profile = new UserProfile
        {
            Id = user.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PrimaryRole = request.Role,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Europe/Prague" : request.TimeZoneId,
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? "cs-CZ" : request.Locale,
            CreatedAtUtc = clock.UtcNow,
            IsOnboarded = true,
        };
        db.UserProfiles.Add(profile);

        if (request.Role == AppRole.Athlete)
        {
            db.AthleteProfiles.Add(new AthleteProfile { UserProfileId = profile.Id, CreatedAtUtc = clock.UtcNow });
        }
        else
        {
            db.CoachProfiles.Add(new CoachProfile { UserProfileId = profile.Id, CreatedAtUtc = clock.UtcNow });
        }

        await db.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, profile, ipAddress, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new AuthenticationFailedException("Neplatný e-mail nebo heslo.");

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            throw new AuthenticationFailedException(
                signInResult.IsLockedOut
                    ? "Účet je dočasně uzamčen po opakovaných neúspěšných pokusech o přihlášení."
                    : "Neplatný e-mail nebo heslo.");
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Id == user.Id, cancellationToken)
            ?? throw new AuthenticationFailedException("Profil uživatele nebyl nalezen.");

        return await IssueTokensAsync(user, profile, ipAddress, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var hash = JwtTokenGenerator.HashToken(refreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new AuthenticationFailedException("Neplatný obnovovací token.");

        if (!existing.IsActive)
        {
            throw new AuthenticationFailedException("Obnovovací token je neplatný nebo vypršel.");
        }

        var user = await userManager.FindByIdAsync(existing.UserId.ToString())
            ?? throw new AuthenticationFailedException("Uživatel nebyl nalezen.");
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Id == user.Id, cancellationToken)
            ?? throw new AuthenticationFailedException("Profil uživatele nebyl nalezen.");

        existing.RevokedAtUtc = clock.UtcNow;
        existing.RevokedByIp = ipAddress;

        var result = await IssueTokensAsync(user, profile, ipAddress, cancellationToken, replaces: existing);
        return result;
    }

    public async Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var hash = JwtTokenGenerator.HashToken(refreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is { RevokedAtUtc: null })
        {
            existing.RevokedAtUtc = clock.UtcNow;
            existing.RevokedByIp = ipAddress;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.RevokedAtUtc = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResult> IssueTokensAsync(
        ApplicationUser user,
        UserProfile profile,
        string? ipAddress,
        CancellationToken cancellationToken,
        RefreshToken? replaces = null)
    {
        var (accessToken, accessExpiresAtUtc) = tokenGenerator.GenerateAccessToken(user.Id, user.Email!, profile.PrimaryRole);

        var rawRefreshToken = JwtTokenGenerator.GenerateRefreshTokenValue();
        var refreshExpiresAtUtc = clock.UtcNow.AddDays(tokenGenerator.RefreshTokenDays);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtTokenGenerator.HashToken(rawRefreshToken),
            CreatedAtUtc = clock.UtcNow,
            ExpiresAtUtc = refreshExpiresAtUtc,
            CreatedByIp = ipAddress,
        };
        db.RefreshTokens.Add(refreshTokenEntity);

        if (replaces is not null)
        {
            replaces.ReplacedByTokenHash = refreshTokenEntity.TokenHash;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResult(
            user.Id,
            user.Email!,
            profile.DisplayName,
            profile.PrimaryRole,
            profile.IsOnboarded,
            accessToken,
            accessExpiresAtUtc,
            rawRefreshToken,
            refreshExpiresAtUtc);
    }
}
