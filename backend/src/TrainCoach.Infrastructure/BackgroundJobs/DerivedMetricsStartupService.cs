using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Execution;

namespace TrainCoach.Infrastructure.BackgroundJobs;

/// <summary>
/// Once per start, shortly after boot: computes best efforts for stored streams that don't have
/// them at the current algorithm version — the history imported before efforts existed, and every
/// stream again after a BestEffortCalculator.Version bump — then rebuilds PeakForm's training load
/// and CTL/ATL for every athlete (so they're current to today). Idempotent, so a restart just resumes.
/// </summary>
public class DerivedMetricsStartupService(IServiceScopeFactory scopeFactory, ILogger<DerivedMetricsStartupService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IBestEffortRecomputeJob>().RunAsync(null, stoppingToken);
            await scope.ServiceProvider.GetRequiredService<TrainCoach.Application.Wellness.ITrainingLoadRecomputeJob>().RunAsync(null, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Výpočet nejlepších výkonů po startu selhal.");
        }
    }
}
