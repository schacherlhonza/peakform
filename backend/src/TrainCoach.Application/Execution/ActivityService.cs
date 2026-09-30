using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

public class ActivityService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IEnumerable<IIntegrationProvider> providers,
    IAccessTokenResolver tokenResolver,
    IDateTimeProvider clock) : IActivityService
{
    public async Task<IReadOnlyList<CompletedActivityDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);

        var query = db.CompletedActivities.Include(a => a.SourceRecords).Include(a => a.AdditionalMetrics).Where(a => a.AthleteUserId == athleteUserId);
        if (from is not null)
        {
            query = query.Where(a => a.StartedAtUtc >= from.Value.ToDateTime(TimeOnly.MinValue));
        }
        if (to is not null)
        {
            query = query.Where(a => a.StartedAtUtc <= to.Value.ToDateTime(TimeOnly.MaxValue));
        }

        var activities = await query.OrderByDescending(a => a.StartedAtUtc).ToListAsync(cancellationToken);
        return activities.Select(ToDto).ToList();
    }

    public const int MaxPageSize = 100;

    public async Task<ActivityListPageDto> SearchForAthleteAsync(Guid athleteUserId, ActivitySearchQuery search, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);

        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, MaxPageSize);

        var query = db.CompletedActivities.Where(a => a.AthleteUserId == athleteUserId);
        if (search.From is { } from)
        {
            query = query.Where(a => a.StartedAtUtc >= from.ToDateTime(TimeOnly.MinValue));
        }
        if (search.To is { } to)
        {
            query = query.Where(a => a.StartedAtUtc <= to.ToDateTime(TimeOnly.MaxValue));
        }
        if (search.Sports is { Count: > 0 } sports)
        {
            query = query.Where(a => sports.Contains(a.Sport));
        }
        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            // ToLower + Contains rather than ILike so it translates on both Npgsql and the
            // SQLite test host.
            var term = search.Search.Trim().ToLower();
            query = query.Where(a => a.Title != null && a.Title.ToLower().Contains(term));
        }

        // Totals over the full filtered set in one aggregate query. Decimals are summed as double:
        // SQLite (test host) can't aggregate decimal columns; the precision loss is irrelevant
        // for display totals.
        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Duration = g.Sum(a => (long)a.DurationSeconds),
                Distance = g.Sum(a => (double?)a.DistanceMeters) ?? 0,
                Elevation = g.Sum(a => (double?)a.ElevationGainMeters) ?? 0,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var items = await query
            .Include(a => a.SourceRecords).Include(a => a.AdditionalMetrics)
            .OrderByDescending(a => a.StartedAtUtc).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ActivityListPageDto(
            items.Select(ToDto).ToList(),
            totals?.Count ?? 0,
            page,
            pageSize,
            new ActivityListSummaryDto(
                totals?.Count ?? 0,
                totals?.Duration ?? 0,
                (decimal)Math.Round(totals?.Distance ?? 0, 1),
                (decimal)Math.Round(totals?.Elevation ?? 0, 1)));
    }

    public async Task<CompletedActivityDto> GetByIdAsync(Guid callerUserId, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadOwnedActivityAsync(activityId, cancellationToken);
        return ToDto(activity);
    }

    public async Task<ActivityStreamsDto?> GetActivityStreamsAsync(Guid callerUserId, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadOwnedActivityAsync(activityId, cancellationToken);

        // A stored stream wins over a live fetch, from whichever source record carries it — after
        // a Strava-archive + intervals.icu merge, the archive's stream serves the activity even
        // when intervals.icu is the primary source.
        var sourceRecordIds = activity.SourceRecords.Select(sr => sr.Id).ToList();
        var stored = await db.ActivityStreams.AsNoTracking()
            .Where(s => sourceRecordIds.Contains(s.ActivitySourceRecordId))
            .OrderByDescending(s => s.ActivitySourceRecordId == activity.PrimarySourceRecordId)
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is not null)
        {
            return ActivityStreamMapping.ToDto(activityId, activity.Sport, stored);
        }

        var provenance = activity.SourceRecords.FirstOrDefault(sr => sr.Id == activity.PrimarySourceRecordId)
            ?? activity.SourceRecords.FirstOrDefault();
        if (provenance is not { ExternalId: { } externalId }
            || MapToProviderType(provenance.Source) is not { } providerType)
        {
            return null;
        }

        var streamProvider = providers.OfType<IActivityStreamProvider>()
            .FirstOrDefault(p => ((IIntegrationProvider)p).ProviderType == providerType);
        if (streamProvider is null)
        {
            return null;
        }

        var accessToken = await tokenResolver.ResolveFreshAccessTokenAsync(activity.AthleteUserId, providerType, cancellationToken);
        if (accessToken is null)
        {
            return null;
        }

        var streams = await streamProvider.FetchActivityStreamsAsync(accessToken, externalId, cancellationToken);
        return streams is null ? null : ToStreamsDto(activityId, activity.Sport, streams);
    }

    /// <summary>Only sources with a real, still-connectable <see cref="IIntegrationProvider"/> can
    /// serve streams on demand (unlike the one-way import in <see cref="SyncOrchestrator"/>, this
    /// needs a live access token) — reverse of <c>SyncOrchestrator.MapToDataSource</c>.</summary>
    private static IntegrationProviderType? MapToProviderType(DataSource source) => source switch
    {
        DataSource.Strava => IntegrationProviderType.Strava,
        DataSource.IntervalsIcu => IntegrationProviderType.IntervalsIcu,
        _ => null,
    };

    public async Task<CompletedActivityDto> CreateManualAsync(Guid callerUserId, CreateManualActivityRequest request, CancellationToken cancellationToken = default)
    {
        // Manual entry is always self-service — an athlete logs their own activity. Unlike
        // viewing (which a coach may also do), no one else may create an activity in someone
        // else's name, so this is a plain identity check rather than the relationship guard.
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Aktivitu lze zapsat pouze pro sebe.");
        }

        var sourceRecord = new ActivitySourceRecord
        {
            Source = DataSource.Manual,
            FetchedAtUtc = clock.UtcNow,
        };

        var activity = new CompletedActivity
        {
            AthleteUserId = request.AthleteUserId,
            PlannedWorkoutId = request.PlannedWorkoutId,
            Sport = request.Sport,
            Title = request.Title,
            StartedAtUtc = request.StartedAtUtc,
            DurationSeconds = request.DurationSeconds,
            DistanceMeters = request.DistanceMeters,
            ElevationGainMeters = request.ElevationGainMeters,
            AverageHeartRateBpm = request.AverageHeartRateBpm,
            MaxHeartRateBpm = request.MaxHeartRateBpm,
            AveragePaceSecondsPerKm = request.AveragePaceSecondsPerKm,
            AveragePowerWatts = request.AveragePowerWatts,
            Calories = request.Calories,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
            PrimarySourceRecordId = sourceRecord.Id,
            SourceRecords = { sourceRecord },
        };

        db.CompletedActivities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(activity);
    }

    public async Task<TrainingFeedbackDto> UpsertFeedbackAsync(Guid callerUserId, UpsertTrainingFeedbackRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Zpětnou vazbu lze zapsat pouze za sebe.");
        }

        var existing = await db.TrainingFeedbacks.FirstOrDefaultAsync(
            f => f.AthleteUserId == request.AthleteUserId && f.Date == request.Date
                 && f.PlannedWorkoutId == request.PlannedWorkoutId,
            cancellationToken);

        if (existing is null)
        {
            existing = new TrainingFeedback
            {
                AthleteUserId = request.AthleteUserId,
                PlannedWorkoutId = request.PlannedWorkoutId,
                Date = request.Date,
                CreatedAtUtc = clock.UtcNow,
            };
            db.TrainingFeedbacks.Add(existing);
        }

        existing.CompletedActivityId = request.CompletedActivityId;
        existing.Rpe = request.Rpe;
        existing.LegsFeeling = request.LegsFeeling;
        existing.OverallRating = request.OverallRating;
        existing.PainNote = request.PainNote;
        existing.FreeText = request.FreeText;
        existing.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToFeedbackDto(existing);
    }

    public async Task<IReadOnlyList<TrainingFeedbackDto>> GetFeedbackForAthleteAsync(Guid athleteUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);

        var query = db.TrainingFeedbacks.Where(f => f.AthleteUserId == athleteUserId);
        if (from is not null) query = query.Where(f => f.Date >= from.Value);
        if (to is not null) query = query.Where(f => f.Date <= to.Value);

        var results = await query.OrderByDescending(f => f.Date).ToListAsync(cancellationToken);
        return results.Select(ToFeedbackDto).ToList();
    }

    private async Task<CompletedActivity> LoadOwnedActivityAsync(Guid activityId, CancellationToken cancellationToken)
    {
        var activity = await db.CompletedActivities.Include(a => a.SourceRecords).Include(a => a.AdditionalMetrics)
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken)
            ?? throw new NotFoundException(nameof(CompletedActivity), activityId);

        await accessGuard.EnsureAthleteAccessAsync(activity.AthleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);
        return activity;
    }

    private static ActivityStreamsDto ToStreamsDto(Guid activityId, SportType sport, ExternalActivityStreams s) => new(
        activityId,
        s.TimeOffsetsSeconds,
        s.HeartRateBpm?.Select(RoundToInt).ToList(),
        ActivityStreamMapping.StepsPerMinute(sport, s.CadenceRpm?.Select(RoundToInt).ToList()),
        s.WattsOutput?.Select(RoundToInt).ToList(),
        s.DistanceMeters?.Select(v => (decimal?)v).ToList(),
        s.AltitudeMeters?.Select(v => (decimal?)v).ToList(),
        s.VelocityMetersPerSecond?.Select(v => v is > 0 ? (int?)Math.Round(1000d / v.Value) : null).ToList(),
        s.GradePercent?.Select(v => (decimal?)v).ToList());

    private static int? RoundToInt(double? value) => value.HasValue ? (int?)Math.Round(value.Value) : null;

    internal static CompletedActivityDto ToDto(CompletedActivity a)
    {
        var primarySource = a.SourceRecords.FirstOrDefault(sr => sr.Id == a.PrimarySourceRecordId)
            ?? a.SourceRecords.FirstOrDefault();
        return new(
            a.Id, a.AthleteUserId, a.PlannedWorkoutId, a.Sport, a.Title, a.StartedAtUtc, a.DurationSeconds,
            a.DistanceMeters, a.ElevationGainMeters, a.AverageHeartRateBpm, a.MaxHeartRateBpm,
            a.AveragePaceSecondsPerKm, a.AveragePowerWatts, a.Calories, primarySource?.Source ?? DataSource.Manual,
            a.AdditionalMetrics.Count > 0 ? a.AdditionalMetrics.Select(m => new ActivityMetricDto(m.MetricType, m.Value, m.Unit)).ToList() : null,
            primarySource?.DeviceName ?? a.SourceRecords.Select(sr => sr.DeviceName).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)));
    }

    private static TrainingFeedbackDto ToFeedbackDto(TrainingFeedback f) => new(
        f.Id, f.AthleteUserId, f.PlannedWorkoutId, f.CompletedActivityId, f.Date, f.Rpe, f.LegsFeeling, f.OverallRating, f.PainNote, f.FreeText);
}
