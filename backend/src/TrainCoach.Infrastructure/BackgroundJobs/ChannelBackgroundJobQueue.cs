using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
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
}
