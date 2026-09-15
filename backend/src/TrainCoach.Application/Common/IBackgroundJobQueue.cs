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
}
