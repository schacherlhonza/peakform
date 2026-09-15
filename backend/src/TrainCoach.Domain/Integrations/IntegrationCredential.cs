using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Integrations;

/// <summary>
/// Encrypted-at-rest OAuth material for one <see cref="IntegrationConnection"/> (1:1). Values
/// are encrypted by Infrastructure using ASP.NET Data Protection before being written here —
/// this entity never sees a plaintext token. See docs/security.md.
/// </summary>
public class IntegrationCredential : Entity
{
    public Guid IntegrationConnectionId { get; set; }
    public IntegrationConnection IntegrationConnection { get; set; } = null!;

    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string? EncryptedRefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? GrantedScope { get; set; }
}
