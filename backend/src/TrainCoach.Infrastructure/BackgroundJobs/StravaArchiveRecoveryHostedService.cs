using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Integrations.StravaArchive;

namespace TrainCoach.Infrastructure.BackgroundJobs;

/// <summary>
/// The in-process job queue isn't durable (docs/decisions/0003), so a restart silently drops any
/// queued/running archive import. On startup — and every few hours after — this marks those as
/// failed (re-running is safe: level-1 dedup) and sweeps archives past their retention window.
/// </summary>
public class StravaArchiveRecoveryHostedService(IServiceScopeFactory scopeFactory, ILogger<StravaArchiveRecoveryHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);
    private bool _startupPassDone;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<IStravaArchiveImportJob>();
                if (!_startupPassDone)
                {
                    await job.RecoverInterruptedAsync(stoppingToken);
                    _startupPassDone = true;
                }
                else
                {
                    await job.SweepExpiredAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Úklid importů archivu ze Stravy selhal.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }
}
