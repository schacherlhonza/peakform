using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Integrations;

public class SyncOrchestrator(
    IApplicationDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    IAccessTokenResolver tokenResolver,
    IDateTimeProvider clock) : ISyncOrchestrator
{
    public async Task RunAsync(Guid integrationConnectionId, SyncTrigger trigger, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections
            .Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.Id == integrationConnectionId, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", integrationConnectionId);

        var run = new SynchronizationRun
        {
            IntegrationConnectionId = integrationConnectionId,
            Trigger = trigger,
            Status = SyncRunStatus.Running,
            StartedAtUtc = clock.UtcNow,
        };
        db.SynchronizationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            if (connection.Credential is null)
            {
                throw new BusinessRuleException("Propojení nemá uložené přihlašovací údaje.");
            }

            var provider = providers.FirstOrDefault(p => p.ProviderType == connection.Provider)
                ?? throw new BusinessRuleException($"Poskytovatel {connection.Provider} není zaregistrován.");

            var accessToken = await tokenResolver.ResolveFreshAccessTokenAsync(connection.AthleteUserId, connection.Provider, cancellationToken)
                ?? throw new BusinessRuleException("Propojení nemá uložené přihlašovací údaje.");

            var sinceUtc = connection.LastSyncedAtUtc ?? clock.UtcNow.AddDays(-30);
            var activities = await provider.FetchRecentActivitiesAsync(accessToken, sinceUtc, cancellationToken);
            run.ItemsFetched = activities.Count;

            var dataSource = MapToDataSource(connection.Provider);

            foreach (var external in activities)
            {
                var alreadyExists = await db.DataProvenances.AnyAsync(
                    p => p.Source == dataSource && p.ExternalId == external.ExternalId, cancellationToken);
                if (alreadyExists)
                {
                    run.ItemsSkippedDuplicate++;
                    continue;
                }

                var activity = new CompletedActivity
                {
                    AthleteUserId = connection.AthleteUserId,
                    Sport = external.Sport,
                    Title = external.Title,
                    StartedAtUtc = external.StartedAtUtc,
                    DurationSeconds = external.DurationSeconds,
                    DistanceMeters = external.DistanceMeters,
                    ElevationGainMeters = external.ElevationGainMeters,
                    AverageHeartRateBpm = external.AverageHeartRateBpm,
                    MaxHeartRateBpm = external.MaxHeartRateBpm,
                    AveragePaceSecondsPerKm = external.AveragePaceSecondsPerKm,
                    AveragePowerWatts = external.AveragePowerWatts,
                    Calories = external.Calories,
                    CreatedAtUtc = clock.UtcNow,
                    Provenance = new DataProvenance
                    {
                        Source = dataSource,
                        ExternalId = external.ExternalId,
                        SynchronizationRunId = run.Id,
                        FetchedAtUtc = clock.UtcNow,
                    },
                };
                db.CompletedActivities.Add(activity);
                run.ItemsCreated++;
            }

            if (provider is IWellnessDataProvider wellnessProvider)
            {
                var samples = await wellnessProvider.FetchWellnessAsync(accessToken, sinceUtc, cancellationToken);
                foreach (var sample in samples)
                {
                    await UpsertWellnessSampleAsync(connection.AthleteUserId, dataSource, sample, cancellationToken);
                    run.ItemsFetched++;
                }
            }

            connection.LastSyncedAtUtc = clock.UtcNow;
            connection.Status = IntegrationConnectionStatus.Connected;

            run.Status = SyncRunStatus.Succeeded;
            run.FinishedAtUtc = clock.UtcNow;
        }
        catch (Exception ex)
        {
            run.Status = SyncRunStatus.Failed;
            run.FinishedAtUtc = clock.UtcNow;
            run.ErrorMessage = ex.Message;
            connection.Status = IntegrationConnectionStatus.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertWellnessSampleAsync(Guid athleteUserId, DataSource source, ExternalWellnessSample sample, CancellationToken cancellationToken)
    {
        if (sample.HrvRmssdMs is { } hrv)
        {
            var existingHrv = await db.HrvMeasurements.FirstOrDefaultAsync(
                h => h.AthleteUserId == athleteUserId && h.Date == sample.Date && h.Source == source, cancellationToken);
            if (existingHrv is null)
            {
                db.HrvMeasurements.Add(new HrvMeasurement { AthleteUserId = athleteUserId, Date = sample.Date, RmssdMs = hrv, Source = source, CreatedAtUtc = clock.UtcNow });
            }
            else
            {
                existingHrv.RmssdMs = hrv;
                existingHrv.UpdatedAtUtc = clock.UtcNow;
            }
        }

        if (sample.RestingHeartRateBpm is not null || sample.ReadinessScore is not null)
        {
            var existingRecovery = await db.RecoveryMetrics.FirstOrDefaultAsync(
                r => r.AthleteUserId == athleteUserId && r.Date == sample.Date && r.Source == source, cancellationToken);
            if (existingRecovery is null)
            {
                db.RecoveryMetrics.Add(new RecoveryMetric
                {
                    AthleteUserId = athleteUserId,
                    Date = sample.Date,
                    RestingHeartRateBpm = sample.RestingHeartRateBpm,
                    ReadinessScore = sample.ReadinessScore,
                    Source = source,
                    CreatedAtUtc = clock.UtcNow,
                });
            }
            else
            {
                existingRecovery.RestingHeartRateBpm = sample.RestingHeartRateBpm;
                existingRecovery.ReadinessScore = sample.ReadinessScore;
                existingRecovery.UpdatedAtUtc = clock.UtcNow;
            }
        }
    }

    private static DataSource MapToDataSource(IntegrationProviderType provider) => provider switch
    {
        IntegrationProviderType.Strava => DataSource.Strava,
        IntegrationProviderType.GarminDemoProvider => DataSource.GarminDemoProvider,
        IntegrationProviderType.MySasyDemoProvider => DataSource.MySasyDemoProvider,
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };
}
