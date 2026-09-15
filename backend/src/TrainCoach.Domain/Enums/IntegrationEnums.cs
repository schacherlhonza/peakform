namespace TrainCoach.Domain.Enums;

public enum IntegrationProviderType
{
    Strava = 1,
    GarminDemoProvider = 2,
    MySasyDemoProvider = 3,
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

public enum SyncTrigger
{
    Manual = 1,
    Scheduled = 2,
    OnConnect = 3,
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

public enum ImportRowStatus
{
    Valid = 1,
    Warning = 2,
    Error = 3,
    DuplicateSkipped = 4,
}
