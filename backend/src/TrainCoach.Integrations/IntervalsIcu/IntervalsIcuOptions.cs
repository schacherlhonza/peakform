namespace TrainCoach.Integrations.IntervalsIcu;

/// <summary>
/// Bound from configuration ("Integrations:IntervalsIcu" — see appsettings.json / .env.example).
/// Left empty by default; the adapter still registers so the app starts, but connecting an
/// intervals.icu account will fail with a clear error until real values are supplied. Unlike
/// Strava, getting these values is NOT instant self-serve: intervals.icu requires submitting a
/// "Request OAuth access" form (intervals.icu/settings/apps) that their team reviews manually and
/// emails back a client id/secret for — see docs/integrations-research.md §5.
/// </summary>
public class IntervalsIcuOptions
{
    public const string SectionName = "Integrations:IntervalsIcu";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
