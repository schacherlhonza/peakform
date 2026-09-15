namespace TrainCoach.Domain.Enums;

public enum ReportType
{
    Morning = 1,
    Evening = 2,
}

/// <summary>
/// Delivery channel abstraction for <see cref="TrainCoach.Domain.Reporting.GeneratedReport"/>.
/// Only InApp is actually wired up in the MVP; the others exist so the delivery contract does
/// not need to change when a real channel is implemented later.
/// </summary>
public enum ReportDeliveryChannel
{
    InApp = 1,
    Email = 2,
    Push = 3,
    Telegram = 4,
}

public enum ReportDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Failed = 3,
    NotConfigured = 4,
}

public enum InsightSeverity
{
    Info = 1,
    Notice = 2,
    Attention = 3,
}
