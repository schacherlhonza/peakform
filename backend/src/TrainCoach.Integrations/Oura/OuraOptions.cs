namespace TrainCoach.Integrations.Oura;

/// <summary>
/// Bound from configuration ("Integrations:Oura" — see appsettings.json / .env.example). Always
/// empty today — no real Oura developer credentials exist for this project. The adapter still
/// registers so the app starts and the architecture is exercised (capability discovery, connector
/// policy defaults), but every real call throws until real values are supplied. See
/// docs/integrations/oura-whoop-activation.md for how to activate.
/// </summary>
public class OuraOptions
{
    public const string SectionName = "Integrations:Oura";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
