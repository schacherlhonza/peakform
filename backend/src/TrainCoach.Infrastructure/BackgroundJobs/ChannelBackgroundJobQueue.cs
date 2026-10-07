using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Application.Reporting;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Infrastructure.BackgroundJobs;

/// <summary>
/// In-process work queue. Each queued item is a closure over the typed request; the reader
/// (<see cref="QueuedHostedService"/>) resolves a fresh DI scope per item and runs it. Not
/// durable across a process restart — acceptable for MVP scale, see docs/decisions/0003.
/// </summary>
public class ChannelBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();

    public ChannelReader<Func<IServiceProvider, CancellationToken, Task>> Reader => _channel.Reader;

    public ValueTask QueueReportGenerationAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var service = services.GetRequiredService<IReportGenerationService>();
            await service.GenerateAsync(athleteUserId, type, date, ct);
        }, cancellationToken);
    }

    public ValueTask QueueSyncRunAsync(Guid integrationConnectionId, SyncTrigger trigger, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var orchestrator = services.GetRequiredService<ISyncOrchestrator>();
            await orchestrator.RunAsync(integrationConnectionId, trigger, ct);
        }, cancellationToken);
    }

    public ValueTask QueueStravaArchiveAnalysisAsync(Guid importId, Uri? url, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<IStravaArchiveImportJob>();
            await job.AnalyzeAsync(importId, url, ct);
        }, cancellationToken);
    }

    public ValueTask QueueDuplicateReevaluationAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<TrainCoach.Application.Integrations.Matching.IPendingDuplicateReevaluationJob>();
            await job.RunAsync(athleteUserId, ct);
        }, cancellationToken);
    }

    public ValueTask QueueTrainingSettingsSyncAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<ITrainingSettingsSyncJob>();
            await job.RunAsync(athleteUserId, ct);
        }, cancellationToken);
    }

    public ValueTask QueuePlannedWorkoutPushAsync(Guid plannedWorkoutId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<IPlannedWorkoutPushJob>();
            await job.RunAsync(plannedWorkoutId, ct);
        }, cancellationToken);
    }

    public ValueTask QueuePlannedWorkoutPushForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<IPlannedWorkoutPushJob>();
            await job.RunForAthleteAsync(athleteUserId, ct);
        }, cancellationToken);
    }

    public ValueTask QueueTrainingLoadRecomputeAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<TrainCoach.Application.Wellness.ITrainingLoadRecomputeJob>();
            await job.RunAsync(athleteUserId, ct);
        }, cancellationToken);
    }

    public ValueTask QueueHrZoneRecomputeAsync(Guid athleteUserId, bool onlyMissing, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<TrainCoach.Application.Execution.IHrZoneRecomputeJob>();
            await job.RunAsync(athleteUserId, onlyMissing, ct);
        }, cancellationToken);
    }

    public ValueTask QueueActivityStreamBackfillAsync(Guid integrationConnectionId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<IActivityStreamBackfillJob>();
            await job.RunAsync(integrationConnectionId, ct);
        }, cancellationToken);
    }

    public ValueTask QueueStravaArchiveImportAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(async (services, ct) =>
        {
            var job = services.GetRequiredService<IStravaArchiveImportJob>();
            await job.ImportAsync(importId, ct);
        }, cancellationToken);
    }
}
