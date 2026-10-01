using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Identity;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Integrations;

public class SyncOrchestrator(
    IApplicationDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    IAccessTokenResolver tokenResolver,
    IActivityIngestionService ingestionService,
    IConnectorPolicyService policyService,
    TrainCoach.Application.Wellness.IDailyMetricSelectionService dailyMetricSelectionService,
    IBackgroundJobQueue jobQueue,
    IDateTimeProvider clock) : ISyncOrchestrator
{
    public async Task RunAsync(Guid integrationConnectionId, SyncTrigger trigger, CancellationToken cancellationToken = default)
    {
        var connection = await db.IntegrationConnections
            .Include(c => c.Credential)
            .FirstOrDefaultAsync(c => c.Id == integrationConnectionId, cancellationToken)
            ?? throw new NotFoundException("IntegrationConnection", integrationConnectionId);

        // Reuse the Pending row IntegrationConnectionService recorded at enqueue time (so the UI
        // could show "queued"); direct callers (tests, ops CLI) have none and get a fresh one.
        var run = await db.SynchronizationRuns
            .Where(r => r.IntegrationConnectionId == integrationConnectionId && r.Status == SyncRunStatus.Pending)
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            run = new SynchronizationRun { IntegrationConnectionId = integrationConnectionId };
            db.SynchronizationRuns.Add(run);
        }
        run.Trigger = trigger;
        run.Status = SyncRunStatus.Running;
        run.StartedAtUtc = clock.UtcNow;
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

            // A history backfill asks for an explicit start; the regular sync continues from the
            // last run (first sync: 30 days back).
            var sinceUtc = run.HistoryFromUtc ?? connection.LastSyncedAtUtc ?? clock.UtcNow.AddDays(-30);
            var dataSource = MapToDataSource(connection.Provider);

            var activityPolicy = await policyService.GetEffectiveModeAsync(connection.AthleteUserId, connection.Provider, DataDomain.Activities, cancellationToken);
            if (activityPolicy != ConnectorMode.Disabled)
            {
                var activities = await provider.FetchRecentActivitiesAsync(accessToken, sinceUtc, cancellationToken);
                run.ItemsFetched += activities.Count;

                foreach (var external in activities)
                {
                    await ProcessActivityAsync(connection.AthleteUserId, external, dataSource, activityPolicy, run, cancellationToken);
                }
            }

            // Isolated from the activities block above: a wellness-side failure (e.g. an
            // unofficial/community-documented field coming back in an unexpected shape — see
            // FlexibleInt32Converter's doc comment for a real example) must never discard
            // already-synced activities or flip the whole connection to Error, since the
            // connection/credential themselves are fine.
            string? wellnessErrorMessage = null;
            if (provider is IWellnessDataProvider wellnessProvider)
            {
                try
                {
                    var samples = await wellnessProvider.FetchWellnessAsync(accessToken, sinceUtc, cancellationToken);
                    var affectedDates = new HashSet<DateOnly>();
                    foreach (var sample in samples)
                    {
                        await UpsertWellnessSampleAsync(connection.AthleteUserId, dataSource, sample, cancellationToken);
                        affectedDates.Add(sample.Date);
                        run.ItemsFetched++;
                    }

                    // Recompute once per athlete+date at the end, not per field, to avoid redundant
                    // recomputation across the several wellness tables one sample can touch.
                    foreach (var date in affectedDates)
                    {
                        await dailyMetricSelectionService.RecomputeForAthleteDayAsync(connection.AthleteUserId, date, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    wellnessErrorMessage = $"Wellness data se nepodařilo zpracovat, aktivity byly synchronizovány normálně: {ex.Message}";
                }
            }

            // Never move the incremental cursor backwards because of a history run.
            if (connection.LastSyncedAtUtc is null || run.HistoryFromUtc is null)
            {
                connection.LastSyncedAtUtc = clock.UtcNow;
            }
            connection.Status = IntegrationConnectionStatus.Connected;

            run.Status = SyncRunStatus.Succeeded;
            run.FinishedAtUtc = clock.UtcNow;
            run.ErrorMessage = wellnessErrorMessage;
        }
        catch (Exception ex)
        {
            run.Status = SyncRunStatus.Failed;
            run.FinishedAtUtc = clock.UtcNow;
            run.ErrorMessage = ex.Message;
            connection.Status = IntegrationConnectionStatus.Error;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Detail streams are fetched in a separate background job (one file download per
        // activity, throttled), so the sync itself stays fast.
        if (run.Status == SyncRunStatus.Succeeded && providers.FirstOrDefault(p => p.ProviderType == connection.Provider) is IActivityFileProvider)
        {
            await jobQueue.QueueActivityStreamBackfillAsync(connection.Id, cancellationToken);
        }
        // New activities change the load, and CTL/ATL decay every day even without any.
        if (run.Status == SyncRunStatus.Succeeded)
        {
            await jobQueue.QueueTrainingLoadRecomputeAsync(connection.AthleteUserId, cancellationToken);
        }
    }

    private async Task ProcessActivityAsync(
        Guid athleteUserId, ExternalActivity external, DataSource dataSource, ConnectorMode policyMode,
        SynchronizationRun run, CancellationToken cancellationToken)
    {
        var outcome = await ingestionService.IngestAsync(
            athleteUserId, external, dataSource, policyMode, new ActivitySourceOrigin(SynchronizationRunId: run.Id), cancellationToken);
        switch (outcome)
        {
            case IngestionOutcome.Created: run.ItemsCreated++; break;
            case IngestionOutcome.Merged: run.ItemsUpdated++; break;
            case IngestionOutcome.FlaggedForReview: run.ItemsCreated++; run.ItemsFlaggedForReview++; break;
            case IngestionOutcome.SkippedDuplicate: run.ItemsSkippedDuplicate++; break;
        }
    }

    private async Task UpsertWellnessSampleAsync(Guid athleteUserId, DataSource source, ExternalWellnessSample sample, CancellationToken cancellationToken)
    {
        if (sample.HrvRmssdMs is not null || sample.HrvSdnnMs is not null)
        {
            var existingHrv = await db.HrvMeasurements.FirstOrDefaultAsync(
                h => h.AthleteUserId == athleteUserId && h.Date == sample.Date && h.Source == source, cancellationToken);
            if (existingHrv is null)
            {
                db.HrvMeasurements.Add(new HrvMeasurement
                {
                    AthleteUserId = athleteUserId,
                    Date = sample.Date,
                    RmssdMs = sample.HrvRmssdMs ?? 0,
                    SdnnMs = sample.HrvSdnnMs,
                    Source = source,
                    CreatedAtUtc = clock.UtcNow,
                });
            }
            else
            {
                if (sample.HrvRmssdMs is { } rmssd)
                {
                    existingHrv.RmssdMs = rmssd;
                }
                existingHrv.SdnnMs = sample.HrvSdnnMs;
                existingHrv.UpdatedAtUtc = clock.UtcNow;
            }
        }

        if (sample.RestingHeartRateBpm is not null || sample.ReadinessScore is not null || sample.StressScore is not null
            || sample.MoodScore is not null || sample.SorenessScore is not null || sample.MotivationScore is not null
            || sample.Steps is not null || sample.SpO2Percent is not null || sample.Vo2Max is not null || sample.HasInjurySignal is not null)
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
                    StressScore = sample.StressScore,
                    MoodScore = sample.MoodScore,
                    SorenessScore = sample.SorenessScore,
                    MotivationScore = sample.MotivationScore,
                    Steps = sample.Steps,
                    SpO2Percent = sample.SpO2Percent,
                    Vo2Max = sample.Vo2Max,
                    HasInjurySignal = sample.HasInjurySignal,
                    Source = source,
                    CreatedAtUtc = clock.UtcNow,
                });
            }
            else
            {
                existingRecovery.RestingHeartRateBpm = sample.RestingHeartRateBpm;
                existingRecovery.ReadinessScore = sample.ReadinessScore;
                existingRecovery.StressScore = sample.StressScore;
                existingRecovery.MoodScore = sample.MoodScore;
                existingRecovery.SorenessScore = sample.SorenessScore;
                existingRecovery.MotivationScore = sample.MotivationScore;
                existingRecovery.Steps = sample.Steps;
                existingRecovery.SpO2Percent = sample.SpO2Percent;
                existingRecovery.Vo2Max = sample.Vo2Max;
                existingRecovery.HasInjurySignal = sample.HasInjurySignal;
                existingRecovery.UpdatedAtUtc = clock.UtcNow;
            }
        }

        if (sample.SleepDurationMinutes is not null || sample.SleepScore is not null || sample.AvgSleepingHeartRateBpm is not null)
        {
            var existingSleep = await db.SleepRecords.FirstOrDefaultAsync(
                s => s.AthleteUserId == athleteUserId && s.Date == sample.Date && s.Source == source, cancellationToken);
            if (existingSleep is null)
            {
                db.SleepRecords.Add(new SleepRecord
                {
                    AthleteUserId = athleteUserId,
                    Date = sample.Date,
                    DurationMinutes = sample.SleepDurationMinutes,
                    SleepScore = sample.SleepScore,
                    AvgSleepingHeartRateBpm = sample.AvgSleepingHeartRateBpm,
                    Source = source,
                    CreatedAtUtc = clock.UtcNow,
                });
            }
            else
            {
                existingSleep.DurationMinutes = sample.SleepDurationMinutes;
                existingSleep.SleepScore = sample.SleepScore;
                existingSleep.AvgSleepingHeartRateBpm = sample.AvgSleepingHeartRateBpm;
                existingSleep.UpdatedAtUtc = clock.UtcNow;
            }
        }

        if (sample.WeightKg is { } weightKg)
        {
            var existingWeight = await db.WeightMeasurements.FirstOrDefaultAsync(
                w => w.AthleteUserId == athleteUserId && w.Date == sample.Date && w.Source == source, cancellationToken);
            if (existingWeight is null)
            {
                db.WeightMeasurements.Add(new WeightMeasurement { AthleteUserId = athleteUserId, Date = sample.Date, WeightKg = weightKg, Source = source, CreatedAtUtc = clock.UtcNow });
            }
            else
            {
                existingWeight.WeightKg = weightKg;
                existingWeight.UpdatedAtUtc = clock.UtcNow;
            }

            // Keep the single-value profile field in sync with whichever measurement is most
            // recent across all sources — not just this sync's own date, since a backfilled or
            // out-of-order sample must not overwrite a genuinely newer reading.
            var latestDate = await db.WeightMeasurements
                .Where(w => w.AthleteUserId == athleteUserId)
                .OrderByDescending(w => w.Date)
                .Select(w => (DateOnly?)w.Date)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestDate is null || latestDate <= sample.Date)
            {
                var profile = await db.AthleteProfiles.FirstOrDefaultAsync(p => p.UserProfileId == athleteUserId, cancellationToken);
                if (profile is not null)
                {
                    profile.CurrentWeightKg = weightKg;
                    profile.UpdatedAtUtc = clock.UtcNow;
                }
            }
        }

        if (sample.Ctl is not null || sample.Atl is not null || sample.RampRate is not null)
        {
            var existingLoad = await db.TrainingLoadSnapshots.FirstOrDefaultAsync(
                t => t.AthleteUserId == athleteUserId && t.Date == sample.Date && t.Source == source, cancellationToken);
            if (existingLoad is null)
            {
                db.TrainingLoadSnapshots.Add(new TrainingLoadSnapshot
                {
                    AthleteUserId = athleteUserId,
                    Date = sample.Date,
                    Ctl = sample.Ctl,
                    Atl = sample.Atl,
                    RampRate = sample.RampRate,
                    Source = source,
                    CreatedAtUtc = clock.UtcNow,
                });
            }
            else
            {
                existingLoad.Ctl = sample.Ctl;
                existingLoad.Atl = sample.Atl;
                existingLoad.RampRate = sample.RampRate;
                existingLoad.UpdatedAtUtc = clock.UtcNow;
            }
        }
    }

    private static DataSource MapToDataSource(IntegrationProviderType provider) => provider switch
    {
        IntegrationProviderType.Strava => DataSource.Strava,
        IntegrationProviderType.GarminDemoProvider => DataSource.GarminDemoProvider,
        IntegrationProviderType.MySasyDemoProvider => DataSource.MySasyDemoProvider,
        IntegrationProviderType.IntervalsIcu => DataSource.IntervalsIcu,
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };
}
