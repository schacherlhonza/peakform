namespace TrainCoach.Domain.Enums;

public enum IntegrationProviderType
{
    Strava = 1,
    GarminDemoProvider = 2,
    MySasyDemoProvider = 3,

    /// <summary>
    /// Real OAuth2 adapter for intervals.icu, which itself aggregates Garmin, Polar, Suunto,
    /// Coros, Huawei, Amazfit, Oura, WHOOP and Strava — see docs/integrations-research.md §5.
    /// Preferred over <see cref="GarminDemoProvider"/> for real (non-demo) Garmin/wellness data,
    /// since Garmin's own Connect Developer Program is business-only and not obtainable here.
    /// </summary>
    IntervalsIcu = 4,

    /// <summary>Architecture-ready only — throws until real OAuth credentials are configured. See
    /// docs/integrations/oura-whoop-activation.md.</summary>
    Oura = 5,

    /// <summary>Architecture-ready only — throws until real OAuth credentials are configured. See
    /// docs/integrations/oura-whoop-activation.md.</summary>
    Whoop = 6,
}

/// <summary>A category of data a connector can supply, independently configurable per provider —
/// see <see cref="TrainCoach.Domain.Integrations.ConnectorDomainPolicy"/>.</summary>
public enum DataDomain
{
    Activities = 1,
    PlannedWorkouts = 2,
    Sleep = 3,
    Hrv = 4,
    RestingHeartRate = 5,
    DailyWellness = 6,
    BodyComposition = 7,
    VendorScores = 8,
}

/// <summary>How much authority a provider has for one <see cref="DataDomain"/>, for one athlete.
/// Never a single global priority across all metrics — see docs/integrations/canonical-data-and-deduplication-plan.md.</summary>
public enum ConnectorMode
{
    Primary = 1,
    Secondary = 2,
    EnrichmentOnly = 3,
    FallbackOnly = 4,
    Disabled = 5,
}

public enum IntegrationConnectionStatus
{
    NotConnected = 1,
    Connected = 2,
    Error = 3,
    Revoked = 4,
}

public enum SyncRunStatus
{
    Pending = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
}

/// <summary>
/// <see cref="Scheduled"/> is reserved for a future periodic/cron sync loop — nothing constructs
/// it today (see docs/integrations/activity-matching.md, "Recommended Next Steps").
/// </summary>
public enum SyncTrigger
{
    Manual = 1,
    Scheduled = 2,
    OnConnect = 3,

    /// <summary>Automatic "sync everything" the frontend fires once per session after sign-in —
    /// throttled server-side so repeated logins/tabs don't hammer provider rate limits.</summary>
    OnLogin = 4,

    /// <summary>Athlete-requested one-off pull of older history, from
    /// <see cref="TrainCoach.Domain.Integrations.SynchronizationRun.HistoryFromUtc"/> instead of
    /// the last sync time.</summary>
    HistoryBackfill = 5,
}

public enum ImportFileType
{
    GoogleSheetsExportCsv = 1,
    StandardCsvTemplate = 2,
    GarminActivityExport = 3,
    MySasyExport = 4,
}

public enum ImportStatus
{
    Uploaded = 1,
    PreviewReady = 2,
    Confirmed = 3,
    Cancelled = 4,
    Failed = 5,
}

/// <summary>Lifecycle of a <see cref="TrainCoach.Domain.Integrations.StravaArchiveImport"/>. The three
/// working states (Downloading, Analyzing, Importing) are what a server restart interrupts.</summary>
public enum StravaArchiveImportStatus
{
    Pending = 1,
    Downloading = 2,
    Analyzing = 3,
    PreviewReady = 4,
    Importing = 5,
    Succeeded = 6,
    Failed = 7,
    Cancelled = 8,
}

public enum StravaArchiveSourceKind
{
    Link = 1,
    Upload = 2,
}

public enum ImportRowStatus
{
    Valid = 1,
    Warning = 2,
    Error = 3,
    DuplicateSkipped = 4,
}
