using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Identity;

/// <summary>
/// One row per issued refresh token (i.e. per active session/device). Only the SHA-256 hash
/// of the token is stored — never the raw value. Rotation replaces a token with a new row and
/// marks this one revoked, recording the chain via <see cref="ReplacedByTokenHash"/>.
/// </summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public string? DeviceInfo { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
