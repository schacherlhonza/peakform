namespace TrainCoach.Integrations.Strava;

/// <summary>
/// Bound from configuration ("Integrations:Strava" — see appsettings.json / .env.example).
/// Left empty by default; the adapter still registers so the app starts, but connecting a
/// Strava account will fail with a clear error until real values are supplied. See
/// docs/integrations-research.md for how to create a Strava API application.
/// </summary>
public class StravaOptions
{
    public const string SectionName = "Integrations:Strava";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
