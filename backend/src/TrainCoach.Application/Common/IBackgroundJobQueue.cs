using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Common;

/// <summary>
/// Enqueues async work to run outside the request. Backed by an in-process
/// System.Threading.Channels queue drained by a BackgroundService — no external job infra
/// (see docs/decisions/0003). Not durable across a restart; acceptable for MVP-scale
/// report generation and sync triggers, documented as swappable for Hangfire/Quartz later.
/// </summary>
public interface IBackgroundJobQueue
{
    ValueTask QueueReportGenerationAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default);

    ValueTask QueueSyncRunAsync(Guid integrationConnectionId, SyncTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>Download (for a link) and dry-run analysis of a Strava archive. The link travels
    /// only inside the queued closure — it's a bearer secret and is never persisted.</summary>
    ValueTask QueueStravaArchiveAnalysisAsync(Guid importId, Uri? url, CancellationToken cancellationToken = default);

    ValueTask QueueStravaArchiveImportAsync(Guid importId, CancellationToken cancellationToken = default);

    ValueTask QueueActivityStreamBackfillAsync(Guid integrationConnectionId, CancellationToken cancellationToken = default);

    ValueTask QueueHrZoneRecomputeAsync(Guid athleteUserId, bool onlyMissing, CancellationToken cancellationToken = default);
}
