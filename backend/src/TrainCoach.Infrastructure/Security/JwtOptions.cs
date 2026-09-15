namespace TrainCoach.Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TrainCoach";
    public string Audience { get; set; } = "TrainCoach.Clients";

    /// <summary>Loaded from configuration/secrets — never hardcoded, never committed. See .env.example.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
