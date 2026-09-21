namespace TrainCoach.Integrations.Whoop;

/// <summary>Same shape/role as OuraOptions — see its doc comment. "Integrations:Whoop" section.</summary>
public class WhoopOptions
{
    public const string SectionName = "Integrations:Whoop";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
