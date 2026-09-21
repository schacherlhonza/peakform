using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations.Matching;
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
    IActivityMatchingService matchingService,
    IConnectorPolicyService policyService,
    TrainCoach.Application.Wellness.IDailyMetricSelectionService dailyMetricSelectionService,
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

            connection.LastSyncedAtUtc = clock.UtcNow;
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
    }

    /// <summary>
    /// One incoming activity through the level-1 (exact external id) check, then — on a miss —
    /// the matcher, then the connector policy for what to do with the outcome. See
    /// docs/integrations/canonical-data-and-deduplication-plan.md for the full pipeline.
    /// </summary>
    private async Task ProcessActivityAsync(
        Guid athleteUserId, ExternalActivity external, DataSource dataSource, ConnectorMode policyMode,
        SynchronizationRun run, CancellationToken cancellationToken)
    {
        // Level 1 — idempotent upsert. The one check that must never be replaced by fuzzy/fingerprint
        // matching.
        var alreadyExists = await db.ActivitySourceRecords.AnyAsync(
            p => p.Source == dataSource && p.ExternalId == external.ExternalId, cancellationToken);
        if (alreadyExists)
        {
            run.ItemsSkippedDuplicate++;
            return;
        }

        var newRecord = BuildSourceRecord(external, dataSource, run.Id);

        // Levels 2-5 — only consulted after the level-1 exact-match check misses.
        var match = await matchingService.FindOrScoreMatchAsync(athleteUserId, external, dataSource, cancellationToken);

        switch (match.Outcome)
        {
            case ActivityMatchOutcome.AutoMerge:
                var existingActivity = await db.CompletedActivities.Include(a => a.SourceRecords)
                    .FirstAsync(a => a.Id == match.MatchedActivityId, cancellationToken);
                AttachAsMergedSource(existingActivity, newRecord, external, policyMode, match, run);
                run.ItemsUpdated++;
                break;

            case ActivityMatchOutcome.FlagForReview:
                // Never silently withhold data — still create the activity, just flagged, and
                // queue it for human review instead of deciding automatically.
                if (policyMode == ConnectorMode.EnrichmentOnly)
                {
                    // An enrichment-only source never creates a new canonical activity on its own;
                    // a review-band candidate with no committed create is simply dropped (logged
                    // via the skip counter) rather than left half-attached.
                    run.ItemsSkippedDuplicate++;
                    break;
                }

                var created = CreateNewActivity(athleteUserId, external, newRecord, dataSource, run);
                created.MatchStatus = ActivityMatchStatus.PendingReview;
                db.DuplicateCandidates.Add(new DuplicateCandidate
                {
                    AthleteUserId = athleteUserId,
                    ActivityAId = match.MatchedActivityId!.Value,
                    ActivityBId = created.Id,
                    ConfidenceScore = match.ConfidenceScore ?? 0,
                    ScoringBreakdownJson = match.ScoringBreakdownJson ?? "{}",
                    Status = DuplicateCandidateStatus.Pending,
                    CreatedAtUtc = clock.UtcNow,
                });
                run.ItemsFlaggedForReview++;
                break;

            case ActivityMatchOutcome.NoCandidate:
            case ActivityMatchOutcome.TreatAsSeparate:
                if (policyMode == ConnectorMode.EnrichmentOnly)
                {
                    // Enrichment-only sources are defined as "only decorates an activity another
                    // source already reported" — with no candidate to decorate, drop the item.
                    run.ItemsSkippedDuplicate++;
                    break;
                }
                CreateNewActivity(athleteUserId, external, newRecord, dataSource, run);
                break;
        }
    }

    private ActivitySourceRecord BuildSourceRecord(ExternalActivity external, DataSource dataSource, Guid runId) => new()
    {
        Source = dataSource,
        ExternalId = external.ExternalId,
        SynchronizationRunId = runId,
        FetchedAtUtc = clock.UtcNow,
        RawPayloadRetained = external.RawPayloadJson is not null,
        RawPayloadJson = external.RawPayloadJson,
        DeviceName = external.DeviceName,
        FitFileUuid = external.FitFileUuid,
        NormalizedFingerprint = ActivityFingerprint.Compute(external.Sport, external.StartedAtUtc, external.DurationSeconds, external.DistanceMeters, external.DeviceName),
    };

    private CompletedActivity CreateNewActivity(Guid athleteUserId, ExternalActivity external, ActivitySourceRecord sourceRecord, DataSource dataSource, SynchronizationRun run)
    {
        var activity = new CompletedActivity
        {
            AthleteUserId = athleteUserId,
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
            NormalizedFingerprint = sourceRecord.NormalizedFingerprint,
            PrimarySourceRecordId = sourceRecord.Id,
            SourceRecords = { sourceRecord },
        };
        db.CompletedActivities.Add(activity);

        if (external.AdditionalMetrics is { Count: > 0 })
        {
            foreach (var metric in external.AdditionalMetrics)
            {
                db.ActivityMetrics.Add(new ActivityMetric
                {
                    CompletedActivity = activity,
                    MetricType = metric.Type,
                    Value = metric.Value,
                    Unit = metric.Unit,
                    Source = dataSource,
                    RecordedAtUtc = clock.UtcNow,
                });
            }
        }

        run.ItemsCreated++;
        return activity;
    }

    /// <summary>
    /// Attaches a newly-matched source record to an already-canonical activity instead of
    /// creating a second one, and decides — from the connector policy in effect — whether this
    /// source's values should become the activity's primary (displayed) values. A Primary-ranked
    /// source always takes over; Secondary only takes over if nothing is primary yet;
    /// EnrichmentOnly/FallbackOnly never promote over an existing primary (this is the concrete
    /// mechanism behind "Strava must not create a second canonical activity for something
    /// intervals.icu already reported").
    /// </summary>
    private void AttachAsMergedSource(CompletedActivity activity, ActivitySourceRecord newRecord, ExternalActivity external, ConnectorMode policyMode, ActivityMatchResult match, SynchronizationRun run)
    {
        // Explicit Add (not just attaching to activity.SourceRecords) — newRecord has a
        // client-generated, non-default Guid key, so if it were only attached via the navigation
        // on an already-Unchanged parent, EF's change-tracker heuristic would mark it Modified
        // (assuming it already exists) instead of Added, generating an UPDATE against a row that
        // was never inserted.
        newRecord.CompletedActivityId = activity.Id;
        db.ActivitySourceRecords.Add(newRecord);

        var shouldPromote = policyMode switch
        {
            ConnectorMode.Primary => true,
            ConnectorMode.Secondary => activity.PrimarySourceRecordId is null,
            _ => false,
        };

        if (shouldPromote)
        {
            activity.Sport = external.Sport;
            activity.Title = external.Title;
            activity.StartedAtUtc = external.StartedAtUtc;
            activity.DurationSeconds = external.DurationSeconds;
            activity.DistanceMeters = external.DistanceMeters;
            activity.ElevationGainMeters = external.ElevationGainMeters;
            activity.AverageHeartRateBpm = external.AverageHeartRateBpm;
            activity.MaxHeartRateBpm = external.MaxHeartRateBpm;
            activity.AveragePaceSecondsPerKm = external.AveragePaceSecondsPerKm;
            activity.AveragePowerWatts = external.AveragePowerWatts;
            activity.Calories = external.Calories;
            activity.PrimarySourceRecordId = newRecord.Id;
            activity.NormalizedFingerprint = newRecord.NormalizedFingerprint;
        }

        activity.MatchStatus = ActivityMatchStatus.AutoMerged;
        activity.UpdatedAtUtc = clock.UtcNow;

        db.MergeDecisions.Add(new MergeDecision
        {
            AthleteUserId = activity.AthleteUserId,
            SurvivingActivityId = activity.Id,
            AbsorbedSourceRecordId = newRecord.Id,
            ConfidenceScore = match.ConfidenceScore ?? 100,
            Kind = match.DecisionKind ?? MergeDecisionKind.AutoFingerprintExact,
            Outcome = MergeDecisionOutcome.Merged,
            ScoringBreakdownJson = match.ScoringBreakdownJson,
            DecidedAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow,
        });
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
