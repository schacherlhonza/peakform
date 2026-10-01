using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public interface IActivityStreamBackfillJob
{
    /// <summary>Downloads and stores detail streams for this connection's activities that don't
    /// have one yet (newest first). Stops early on the provider's rate limit; the next sync
    /// queues it again and it resumes where it stopped.</summary>
    Task RunAsync(Guid integrationConnectionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fills <c>ActivityStream</c> from providers that can hand over the original device file
/// (<see cref="IActivityFileProvider"/> — intervals.icu). Queued after every successful sync of
/// such a connection. Each record is attempted once (<c>StreamFetchAttemptedAtUtc</c>), so
/// activities without a usable file don't cost a request on every sync; activities that already
/// have a stream from any source (e.g. the Strava archive) are skipped entirely.
/// </summary>
public class ActivityStreamBackfillJob(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IIntegrationProvider> providers,
    IActivityFileProbe fileProbe,
    IBackgroundJobQueue jobQueue,
    IDateTimeProvider clock,
    ILogger<ActivityStreamBackfillJob> logger) : IActivityStreamBackfillJob
{
    private const int PageSize = 50;
    private const int MaxPerRun = 3000;
    private const int MaxConsecutiveErrors = 5;

    /// <summary>Stays well under intervals.icu's 10 requests/s per IP.</summary>
    internal static TimeSpan DelayBetweenDownloads { get; set; } = TimeSpan.FromMilliseconds(200);

    private static readonly ConcurrentDictionary<Guid, byte> Running = new();

    public async Task RunAsync(Guid integrationConnectionId, CancellationToken cancellationToken = default)
    {
        if (!Running.TryAdd(integrationConnectionId, 0))
        {
            return; // a previous run for this connection is still going
        }

        try
        {
            await RunCoreAsync(integrationConnectionId, cancellationToken);
        }
        finally
        {
            Running.TryRemove(integrationConnectionId, out _);
        }
    }

    private async Task RunCoreAsync(Guid integrationConnectionId, CancellationToken cancellationToken)
    {
        Guid athleteUserId;
        IntegrationProviderType providerType;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var connection = await db.IntegrationConnections.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == integrationConnectionId, cancellationToken);
            if (connection is null || connection.Status != IntegrationConnectionStatus.Connected)
            {
                return;
            }
            athleteUserId = connection.AthleteUserId;
            providerType = connection.Provider;
        }

        if (providers.FirstOrDefault(p => p.ProviderType == providerType) is not IActivityFileProvider fileProvider
            || ToDataSource(providerType) is not { } source)
        {
            return;
        }

        string? accessToken;
        using (var scope = scopeFactory.CreateScope())
        {
            accessToken = await scope.ServiceProvider.GetRequiredService<IAccessTokenResolver>()
                .ResolveFreshAccessTokenAsync(athleteUserId, providerType, cancellationToken);
        }
        if (accessToken is null)
        {
            return;
        }

        int attempted = 0, stored = 0, consecutiveErrors = 0;
        while (attempted < MaxPerRun)
        {
            var page = await NextCandidatesAsync(athleteUserId, source, cancellationToken);
            if (page.Count == 0)
            {
                break;
            }

            foreach (var (recordId, externalId) in page)
            {
                byte[]? file;
                try
                {
                    file = await fileProvider.DownloadActivityFileAsync(accessToken, externalId, cancellationToken);
                    consecutiveErrors = 0;
                }
                catch (ProviderRateLimitedException)
                {
                    logger.LogInformation("Doplňování streamů ({Provider}) narazilo na limit API po {Attempted} aktivitách, pokračuje při další synchronizaci.", providerType, attempted);
                    await QueueZonesIfStoredAsync(athleteUserId, stored, cancellationToken);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Transient (network, 5xx): don't mark the record, just try the next one —
                    // but give up for this run if the provider keeps failing.
                    logger.LogWarning(ex, "Soubor aktivity {ExternalId} z {Provider} se nepodařilo stáhnout.", externalId, providerType);
                    if (++consecutiveErrors >= MaxConsecutiveErrors)
                    {
                        await QueueZonesIfStoredAsync(athleteUserId, stored, cancellationToken);
                        return;
                    }
                    continue;
                }

                attempted++;
                if (await StoreAsync(recordId, file, source, cancellationToken))
                {
                    stored++;
                }
                await Task.Delay(DelayBetweenDownloads, cancellationToken);
            }
        }

        if (attempted > 0)
        {
            logger.LogInformation("Doplňování streamů ({Provider}): staženo {Attempted} souborů, uloženo {Stored} streamů.", providerType, attempted, stored);
        }
        await QueueZonesIfStoredAsync(athleteUserId, stored, cancellationToken);
    }

    private async Task QueueZonesIfStoredAsync(Guid athleteUserId, int stored, CancellationToken cancellationToken)
    {
        if (stored > 0)
        {
            await jobQueue.QueueHrZoneRecomputeAsync(athleteUserId, onlyMissing: true, cancellationToken);
            await jobQueue.QueueTrainingLoadRecomputeAsync(athleteUserId, cancellationToken);
        }
    }

    private async Task<List<(Guid RecordId, string ExternalId)>> NextCandidatesAsync(Guid athleteUserId, DataSource source, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var rows = await db.ActivitySourceRecords.AsNoTracking()
            .Where(sr => sr.Source == source && sr.ExternalId != null && sr.StreamFetchAttemptedAtUtc == null
                && sr.CompletedActivity.AthleteUserId == athleteUserId
                && !sr.CompletedActivity.SourceRecords.Any(other => other.Stream != null))
            .OrderByDescending(sr => sr.CompletedActivity.StartedAtUtc)
            .Take(PageSize)
            .Select(sr => new { sr.Id, sr.ExternalId })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.ExternalId!)).ToList();
    }

    /// <summary>Marks the record as attempted and, when the file parses into a stream, stores it —
    /// plus the FIT file identity (level-3 match key) if the record didn't have one.</summary>
    private async Task<bool> StoreAsync(Guid recordId, byte[]? file, DataSource source, CancellationToken cancellationToken)
    {
        ActivityStreamData? stream = null;
        string? fitFileUuid = null;
        if (file is not null && ActivityFileFormatDetector.Detect(file) is { } detected)
        {
            stream = fileProbe.ReadStream(new MemoryStream(detected.Content), detected.Format);
            fitFileUuid = fileProbe.Probe(new MemoryStream(detected.Content), detected.Format, identityOnly: true)?.FitFileUuid;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var record = await db.ActivitySourceRecords.Include(sr => sr.Stream).Include(sr => sr.CompletedActivity)
            .FirstOrDefaultAsync(sr => sr.Id == recordId, cancellationToken);
        if (record is null)
        {
            return false;
        }

        record.StreamFetchAttemptedAtUtc = clock.UtcNow;
        record.FitFileUuid ??= fitFileUuid;
        var added = false;
        if (stream is not null && record.Stream is null)
        {
            var entity = ActivityStreamMapping.ToEntity(record.Id, ActivityStreamDownsampler.Downsample(stream), ToOrigin(source), clock.UtcNow);
            db.ActivityStreams.Add(entity);
            // Precise best efforts from the full-resolution file, before it's thrown away.
            await BestEffortStore.ReplaceAsync(db, record.CompletedActivity,
                BestEffortCalculator.Compute(record.CompletedActivity.Sport, stream), precise: true, [entity], clock.UtcNow, cancellationToken);
            added = true;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return added;
        }
        catch (DbUpdateException ex)
        {
            // A concurrent run stored the same stream first (unique index) — nothing lost.
            logger.LogDebug(ex, "Stream pro záznam {RecordId} už existuje.", recordId);
            return false;
        }
    }

    private static DataSource? ToDataSource(IntegrationProviderType provider) => provider switch
    {
        IntegrationProviderType.IntervalsIcu => DataSource.IntervalsIcu,
        _ => null,
    };

    private static ActivityStreamOrigin ToOrigin(DataSource source) => source switch
    {
        DataSource.IntervalsIcu => ActivityStreamOrigin.IntervalsIcuFile,
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
