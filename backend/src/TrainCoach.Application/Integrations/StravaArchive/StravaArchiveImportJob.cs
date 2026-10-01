using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;

namespace TrainCoach.Application.Integrations.StravaArchive;

/// <summary>
/// Background side of the Strava archive import. Each batch of activities runs in its own DI scope
/// (fresh DbContext), so tracking thousands of activities never slows the change tracker down and a
/// failed batch can't poison the next one. Progress is saved per batch — which is also where a
/// cancel requested from the API (status flipped to Cancelled) is noticed.
/// </summary>
public class StravaArchiveImportJob(
    IServiceScopeFactory scopeFactory,
    IStravaArchiveFileStore fileStore,
    IStravaArchiveDownloader downloader,
    IActivityFileProbe fileProbe,
    IOptions<StravaArchiveImportOptions> options,
    IBackgroundJobQueue jobQueue,
    IDateTimeProvider clock,
    ILogger<StravaArchiveImportJob> logger) : IStravaArchiveImportJob
{
    private const int AnalyzeBatchSize = 200;
    private const int ImportBatchSize = 50;

    private sealed class ImportCancelledException : Exception;

    public async Task AnalyzeAsync(Guid importId, Uri? url, CancellationToken cancellationToken = default)
    {
        var import = await LoadAsync(importId, cancellationToken);
        if (import is null || import.Status != StravaArchiveImportStatus.Pending)
        {
            return;
        }

        try
        {
            if (import.SourceKind == StravaArchiveSourceKind.Link)
            {
                if (url is null)
                {
                    throw new BusinessRuleException("Odkaz na archiv už není k dispozici, spusťte import znovu.");
                }
                await DownloadAsync(import, url, cancellationToken);
            }
            else
            {
                await UpdateAsync(importId, i =>
                {
                    i.Status = StravaArchiveImportStatus.Analyzing;
                    i.StartedAtUtc = clock.UtcNow;
                }, cancellationToken);
            }

            import = (await LoadAsync(importId, cancellationToken))!;
            await EnsureArchiveBelongsToAthleteAsync(import, cancellationToken);
            await AnalyzeArchiveAsync(import, cancellationToken);
        }
        catch (ImportCancelledException)
        {
            DeleteArchive(import);
        }
        catch (Exception ex) when (ex is BusinessRuleException or StravaArchiveFormatException)
        {
            await FailAsync(importId, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Analýza archivu ze Stravy {ImportId} selhala.", importId);
            await FailAsync(importId, "Archiv se nepodařilo zpracovat kvůli neočekávané chybě.", cancellationToken);
        }
    }

    public async Task ImportAsync(Guid importId, CancellationToken cancellationToken = default)
    {
        var import = await LoadAsync(importId, cancellationToken);
        if (import is null || import.Status != StravaArchiveImportStatus.Importing || import.StorageKey is null)
        {
            return;
        }

        try
        {
            var mode = await ResolvePolicyModeAsync(import.AthleteUserId, cancellationToken);
            var filter = new ActivityFilter(import);
            var seenIds = new HashSet<string>();
            var counts = new Counts();

            var activities = StravaArchiveReader.ReadActivities(fileStore.GetPath(import.StorageKey), fileProbe, cancellationToken, includeStreams: true);
            foreach (var batch in activities.Chunk(ImportBatchSize))
            {
                await ImportBatchAsync(import, batch, mode, filter, seenIds, counts, cancellationToken);
            }

            await UpdateAsync(importId, i =>
            {
                i.Status = StravaArchiveImportStatus.Succeeded;
                i.FinishedAtUtc = clock.UtcNow;
                i.StorageKey = null;
            }, cancellationToken, ignoreCancel: true);
            fileStore.Delete(import.StorageKey);

            if (counts.Streams > 0)
            {
                await jobQueue.QueueHrZoneRecomputeAsync(import.AthleteUserId, onlyMissing: true, cancellationToken);
            }
            await jobQueue.QueueTrainingLoadRecomputeAsync(import.AthleteUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is BusinessRuleException or StravaArchiveFormatException)
        {
            await FailAsync(importId, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Import archivu ze Stravy {ImportId} selhal.", importId);
            await FailAsync(importId, "Import se nepodařilo dokončit kvůli neočekávané chybě. Už naimportované aktivity zůstávají; import můžete spustit znovu, duplicity se nevytvoří.", cancellationToken);
        }
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var interrupted = await db.StravaArchiveImports
                .Where(i => i.Status == StravaArchiveImportStatus.Pending || i.Status == StravaArchiveImportStatus.Downloading
                    || i.Status == StravaArchiveImportStatus.Analyzing || i.Status == StravaArchiveImportStatus.Importing)
                .ToListAsync(cancellationToken);
            MarkFailed(interrupted, "Import byl přerušen restartem serveru. Spusťte ho znovu – už naimportované aktivity se nezduplikují.");
            await db.SaveChangesAsync(cancellationToken);
            if (interrupted.Count > 0)
            {
                logger.LogInformation("{Count} importů archivu ze Stravy přerušil restart serveru, označeny jako selhané.", interrupted.Count);
            }
        }

        await SweepExpiredAsync(cancellationToken);
    }

    public async Task SweepExpiredAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var expiry = clock.UtcNow.AddHours(-options.Value.RetentionHours);

        var expiredPreviews = await db.StravaArchiveImports
            .Where(i => i.Status == StravaArchiveImportStatus.PreviewReady && i.PreviewReadyAtUtc < expiry)
            .ToListAsync(cancellationToken);
        MarkFailed(expiredPreviews, "Náhled nebyl včas potvrzen a archiv byl smazán. Spusťte import znovu.");
        await db.SaveChangesAsync(cancellationToken);

        var keep = (await db.StravaArchiveImports
                .Where(i => i.StorageKey != null && StravaArchiveImportService.ActiveStatuses.Contains(i.Status))
                .Select(i => i.StorageKey!)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var deleted = fileStore.DeleteOlderThan(expiry, keep);
        if (expiredPreviews.Count > 0 || deleted > 0)
        {
            logger.LogInformation("Úklid importů ze Stravy: {Expired} propadlých náhledů, {Deleted} smazaných archivů.", expiredPreviews.Count, deleted);
        }
    }

    private void MarkFailed(IEnumerable<StravaArchiveImport> imports, string message)
    {
        foreach (var import in imports)
        {
            import.Status = StravaArchiveImportStatus.Failed;
            import.ErrorMessage = message;
            import.FinishedAtUtc = clock.UtcNow;
            if (import.StorageKey is not null)
            {
                fileStore.Delete(import.StorageKey);
                import.StorageKey = null;
            }
        }
    }

    private async Task DownloadAsync(StravaArchiveImport import, Uri url, CancellationToken cancellationToken)
    {
        var key = fileStore.CreateKey();
        await UpdateAsync(import.Id, i =>
        {
            i.Status = StravaArchiveImportStatus.Downloading;
            i.StartedAtUtc = clock.UtcNow;
            i.StorageKey = key;
        }, cancellationToken);
        import.StorageKey = key;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(options.Value.DownloadTimeoutMinutes));

        var lastReport = DateTime.MinValue;
        StravaArchiveDownloadResult result;
        try
        {
            await using var destination = fileStore.OpenWrite(key);
            result = await downloader.DownloadAsync(url, destination, options.Value.MaxArchiveBytes, async (downloaded, total) =>
            {
                if (clock.UtcNow - lastReport < TimeSpan.FromSeconds(2))
                {
                    return;
                }
                lastReport = clock.UtcNow;
                await UpdateAsync(import.Id, i =>
                {
                    i.DownloadedBytes = downloaded;
                    i.SizeBytes = total;
                }, cancellationToken);
            }, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new BusinessRuleException("Stahování archivu trvalo příliš dlouho. Zkuste to znovu nebo archiv stáhněte a nahrajte ručně.");
        }

        await UpdateAsync(import.Id, i =>
        {
            i.DownloadedBytes = result.Bytes;
            i.SizeBytes = result.Bytes;
            i.StravaAthleteId ??= result.StravaAthleteId;
            i.Status = StravaArchiveImportStatus.Analyzing;
        }, cancellationToken);
    }

    /// <summary>The archive's athlete id must match the connected Strava account, when both are
    /// known — guards against importing someone else's history by mistake. The id comes from the
    /// archive's own profile.csv; the S3 path / export_{id}.zip name is only a fallback (a file
    /// name can be renamed). Without a Strava connection there's nothing to compare against.</summary>
    private async Task EnsureArchiveBelongsToAthleteAsync(StravaArchiveImport import, CancellationToken cancellationToken)
    {
        if (StravaArchiveReader.ReadAthleteId(fileStore.GetPath(import.StorageKey!)) is { } profileAthleteId
            && profileAthleteId != import.StravaAthleteId)
        {
            import.StravaAthleteId = profileAthleteId;
            await UpdateAsync(import.Id, i => i.StravaAthleteId = profileAthleteId, cancellationToken);
        }

        if (import.StravaAthleteId is null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var connectedAccountId = await db.IntegrationConnections
            .Where(c => c.AthleteUserId == import.AthleteUserId && c.Provider == IntegrationProviderType.Strava && c.ExternalAccountId != null)
            .Select(c => c.ExternalAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (connectedAccountId is not null && connectedAccountId != import.StravaAthleteId)
        {
            throw new BusinessRuleException("Archiv patří jinému účtu Strava, než který máte propojený. Importovat lze jen vlastní export.");
        }
    }

    private async Task AnalyzeArchiveAsync(StravaArchiveImport import, CancellationToken cancellationToken)
    {
        var path = fileStore.GetPath(import.StorageKey!);
        var total = StravaArchiveReader.CountActivities(path);
        await UpdateAsync(import.Id, i => i.PreviewTotal = total, cancellationToken);

        var mode = await ResolvePolicyModeAsync(import.AthleteUserId, cancellationToken);
        var filter = new ActivityFilter(import);
        int processed = 0, inFilter = 0, already = 0, create = 0, merge = 0, review = 0, failed = 0, streams = 0;

        foreach (var batch in StravaArchiveReader.ReadActivities(path, fileProbe, cancellationToken).Chunk(AnalyzeBatchSize))
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var ingestion = scope.ServiceProvider.GetRequiredService<IActivityIngestionService>();
                var withStream = await ExternalIdsWithStreamAsync(
                    scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), import.AthleteUserId,
                    batch.Where(b => b.Activity is not null).Select(b => b.Activity!.ExternalId).ToList(), cancellationToken);
                foreach (var item in batch)
                {
                    processed++;
                    if (item.Activity is null)
                    {
                        failed++;
                        continue;
                    }
                    if (!filter.Includes(item.Activity))
                    {
                        continue;
                    }

                    inFilter++;
                    if (item.Row.FileName is not null && !withStream.Contains(item.Activity.ExternalId))
                    {
                        streams++;
                    }
                    var outcome = await ingestion.PreviewAsync(import.AthleteUserId, item.Activity, DataSource.Strava, mode, cancellationToken);
                    switch (outcome)
                    {
                        case IngestionOutcome.SkippedDuplicate: already++; break;
                        case IngestionOutcome.Created: create++; break;
                        case IngestionOutcome.Merged: merge++; break;
                        case IngestionOutcome.FlaggedForReview: review++; break;
                    }
                }
            }

            await UpdateAsync(import.Id, i =>
            {
                i.ItemsProcessed = processed;
                i.ItemsFailed = failed;
                i.PreviewInFilter = inFilter;
            }, cancellationToken);
        }

        await UpdateAsync(import.Id, i =>
        {
            i.Status = StravaArchiveImportStatus.PreviewReady;
            i.PreviewReadyAtUtc = clock.UtcNow;
            i.ItemsProcessed = processed;
            i.ItemsFailed = failed;
            i.PreviewInFilter = inFilter;
            i.PreviewAlreadyImported = already;
            i.PreviewWouldCreate = create;
            i.PreviewWouldMerge = merge;
            i.PreviewWouldReview = review;
            i.PreviewStreamsToAdd = streams;
        }, cancellationToken);
    }

    private sealed class Counts
    {
        public int Processed, Created, Merged, Review, Skipped, Failed, Streams;
    }

    private async Task ImportBatchAsync(
        StravaArchiveImport import, StravaArchiveActivity[] batch, ConnectorMode mode, ActivityFilter filter,
        HashSet<string> seenIds, Counts counts, CancellationToken cancellationToken)
    {
        var toIngest = new List<StravaArchiveActivity>();
        foreach (var item in batch)
        {
            counts.Processed++;
            if (item.Activity is null)
            {
                counts.Failed++;
            }
            else if (filter.Includes(item.Activity))
            {
                if (seenIds.Add(item.Activity.ExternalId))
                {
                    toIngest.Add(item);
                }
                else
                {
                    counts.Skipped++;
                }
            }
        }

        var origin = new ActivitySourceOrigin(StravaArchiveImportId: import.Id);
        try
        {
            // Whole batch in one scope/transaction: activities + progress saved together.
            using var scope = scopeFactory.CreateScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<IActivityIngestionService>();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var batchCounts = new Counts();
            foreach (var item in toIngest)
            {
                Tally(batchCounts, await ingestion.IngestAsync(import.AthleteUserId, item.Activity!, DataSource.Strava, mode, origin, cancellationToken));
            }
            await db.SaveChangesAsync(cancellationToken);
            batchCounts.Streams = await AttachStreamsAsync(db, import.AthleteUserId, toIngest, cancellationToken);
            await SaveProgressAsync(db, import.Id, counts, batchCounts, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Rare: typically a live Strava sync inserting the same activity concurrently (unique
            // (Source, ExternalId) index). Redo this batch one activity at a time so one conflict
            // doesn't cost the other 49.
            var batchCounts = new Counts();
            foreach (var item in toIngest)
            {
                using var scope = scopeFactory.CreateScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<IActivityIngestionService>();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                try
                {
                    var outcome = await ingestion.IngestAsync(import.AthleteUserId, item.Activity!, DataSource.Strava, mode, origin, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    Tally(batchCounts, outcome);
                    batchCounts.Streams += await AttachStreamsAsync(db, import.AthleteUserId, [item], cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    logger.LogWarning(ex, "Aktivitu {ExternalId} z archivu ze Stravy se nepodařilo uložit.", item.Activity!.ExternalId);
                    batchCounts.Failed++;
                }
            }

            using var progressScope = scopeFactory.CreateScope();
            await SaveProgressAsync(progressScope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), import.Id, counts, batchCounts, cancellationToken);
        }
    }

    /// <summary>
    /// Stores each item's detail stream on its Strava source record, when that record doesn't have
    /// one yet — independent of the ingestion outcome, so re-importing an archive also backfills
    /// streams for activities that already existed (from an earlier import or the live API sync).
    /// Items the policy dropped (no source record) are simply skipped. Returns how many were added.
    /// </summary>
    private async Task<int> AttachStreamsAsync(
        IApplicationDbContext db, Guid athleteUserId, IReadOnlyList<StravaArchiveActivity> items, CancellationToken cancellationToken)
    {
        var withStream = items.Where(i => i.Stream is not null && i.Activity is not null).ToList();
        if (withStream.Count == 0)
        {
            return 0;
        }

        var ids = withStream.Select(i => i.Activity!.ExternalId).ToList();
        var records = await db.ActivitySourceRecords
            .Include(sr => sr.Stream)
            .Include(sr => sr.CompletedActivity)
            .Where(sr => sr.Source == DataSource.Strava && sr.ExternalId != null && ids.Contains(sr.ExternalId)
                && sr.CompletedActivity.AthleteUserId == athleteUserId)
            .ToListAsync(cancellationToken);
        if (records.Count == 0)
        {
            return 0;
        }

        var byExternalId = withStream.ToDictionary(i => i.Activity!.ExternalId);
        var added = 0;
        foreach (var record in records)
        {
            var item = byExternalId[record.ExternalId!];
            if (record.Stream is null)
            {
                record.Stream = ActivityStreamMapping.ToEntity(record.Id, item.Stream!, ActivityStreamOrigin.StravaArchive, clock.UtcNow);
                db.ActivityStreams.Add(record.Stream);
                added++;
            }
            // Best efforts from the full-resolution file — also replaces estimates an earlier pass
            // computed from the downsampled stream (the "precise on the next import" promise).
            if (item.BestEfforts is { } efforts
                && !(record.Stream.BestEffortsPrecise && record.Stream.BestEffortsVersion >= BestEffortCalculator.Version))
            {
                await BestEffortStore.ReplaceAsync(db, record.CompletedActivity, efforts, precise: true, [record.Stream], clock.UtcNow, cancellationToken);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return added;
    }

    private static async Task<HashSet<string>> ExternalIdsWithStreamAsync(
        IApplicationDbContext db, Guid athleteUserId, List<string> externalIds, CancellationToken cancellationToken) =>
        (await db.ActivityStreams
            .Where(s => s.ActivitySourceRecord.Source == DataSource.Strava && externalIds.Contains(s.ActivitySourceRecord.ExternalId!)
                && s.ActivitySourceRecord.CompletedActivity.AthleteUserId == athleteUserId)
            .Select(s => s.ActivitySourceRecord.ExternalId!)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private static void Tally(Counts counts, IngestionOutcome outcome)
    {
        switch (outcome)
        {
            case IngestionOutcome.Created: counts.Created++; break;
            case IngestionOutcome.Merged: counts.Merged++; break;
            case IngestionOutcome.FlaggedForReview: counts.Created++; counts.Review++; break;
            case IngestionOutcome.SkippedDuplicate: counts.Skipped++; break;
        }
    }

    private async Task SaveProgressAsync(IApplicationDbContext db, Guid importId, Counts totals, Counts batch, CancellationToken cancellationToken)
    {
        totals.Created += batch.Created;
        totals.Merged += batch.Merged;
        totals.Review += batch.Review;
        totals.Skipped += batch.Skipped;
        totals.Failed += batch.Failed;
        totals.Streams += batch.Streams;

        var import = await db.StravaArchiveImports.FirstAsync(i => i.Id == importId, cancellationToken);
        import.ItemsProcessed = totals.Processed;
        import.ItemsCreated = totals.Created;
        import.ItemsMerged = totals.Merged;
        import.ItemsFlaggedForReview = totals.Review;
        import.ItemsSkippedDuplicate = totals.Skipped;
        import.ItemsFailed = totals.Failed;
        import.ItemsStreamsAdded = totals.Streams;
        import.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The archive is Strava's data, so it's ingested under Strava's Activities policy —
    /// e.g. FallbackOnly when intervals.icu is connected, so it never overrides intervals.icu's
    /// values. A Disabled policy still yields FallbackOnly: the import is an explicit request.</summary>
    private async Task<ConnectorMode> ResolvePolicyModeAsync(Guid athleteUserId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var policy = scope.ServiceProvider.GetRequiredService<IConnectorPolicyService>();
        var mode = await policy.GetEffectiveModeAsync(athleteUserId, IntegrationProviderType.Strava, DataDomain.Activities, cancellationToken);
        return mode == ConnectorMode.Disabled ? ConnectorMode.FallbackOnly : mode;
    }

    private async Task<StravaArchiveImport?> LoadAsync(Guid importId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.StravaArchiveImports.AsNoTracking().FirstOrDefaultAsync(i => i.Id == importId, cancellationToken);
    }

    /// <summary>Applies <paramref name="mutate"/> in a fresh scope. Throws
    /// <see cref="ImportCancelledException"/> when the athlete cancelled meanwhile.</summary>
    private async Task UpdateAsync(Guid importId, Action<StravaArchiveImport> mutate, CancellationToken cancellationToken, bool ignoreCancel = false)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var import = await db.StravaArchiveImports.FirstAsync(i => i.Id == importId, cancellationToken);
        if (import.Status == StravaArchiveImportStatus.Cancelled && !ignoreCancel)
        {
            throw new ImportCancelledException();
        }
        mutate(import);
        import.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(Guid importId, string message, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var import = await db.StravaArchiveImports.FirstOrDefaultAsync(i => i.Id == importId, cancellationToken);
        if (import is null)
        {
            return;
        }
        DeleteArchive(import);
        if (import.Status != StravaArchiveImportStatus.Cancelled)
        {
            import.Status = StravaArchiveImportStatus.Failed;
            import.ErrorMessage = message;
            import.FinishedAtUtc = clock.UtcNow;
        }
        import.StorageKey = null;
        import.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private void DeleteArchive(StravaArchiveImport? import)
    {
        if (import?.StorageKey is not null)
        {
            fileStore.Delete(import.StorageKey);
        }
    }

    private sealed class ActivityFilter(StravaArchiveImport import)
    {
        private readonly HashSet<SportType> _sports = StravaArchiveImportService.ParseSports(import.SportsFilter).ToHashSet();

        public bool Includes(ExternalActivity activity)
        {
            var date = DateOnly.FromDateTime(activity.StartedAtUtc);
            return (import.FromDate is null || date >= import.FromDate)
                && (import.ToDate is null || date <= import.ToDate)
                && (_sports.Count == 0 || _sports.Contains(activity.Sport));
        }
    }
}
