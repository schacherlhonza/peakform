using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
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

        var query = db.CompletedActivities.Include(a => a.Provenance).Where(a => a.AthleteUserId == athleteUserId);
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

    public async Task<CompletedActivityDto> GetByIdAsync(Guid callerUserId, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadOwnedActivityAsync(activityId, cancellationToken);
        return ToDto(activity);
    }

    public async Task<ActivityStreamsDto?> GetActivityStreamsAsync(Guid callerUserId, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadOwnedActivityAsync(activityId, cancellationToken);

        if (activity.Provenance is not { Source: DataSource.Strava, ExternalId: { } externalId })
        {
            return null;
        }

        var streamProvider = providers.OfType<IActivityStreamProvider>()
            .FirstOrDefault(p => ((IIntegrationProvider)p).ProviderType == IntegrationProviderType.Strava);
        if (streamProvider is null)
        {
            return null;
        }

        var accessToken = await tokenResolver.ResolveFreshAccessTokenAsync(activity.AthleteUserId, IntegrationProviderType.Strava, cancellationToken);
        if (accessToken is null)
        {
            return null;
        }

        var streams = await streamProvider.FetchActivityStreamsAsync(accessToken, externalId, cancellationToken);
        return streams is null ? null : ToStreamsDto(activityId, streams);
    }

    public async Task<CompletedActivityDto> CreateManualAsync(Guid callerUserId, CreateManualActivityRequest request, CancellationToken cancellationToken = default)
    {
        // Manual entry is always self-service — an athlete logs their own activity. Unlike
        // viewing (which a coach may also do), no one else may create an activity in someone
        // else's name, so this is a plain identity check rather than the relationship guard.
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Aktivitu lze zapsat pouze pro sebe.");
        }

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
            Provenance = new DataProvenance
            {
                Source = DataSource.Manual,
                FetchedAtUtc = clock.UtcNow,
            },
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
        var activity = await db.CompletedActivities.Include(a => a.Provenance)
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken)
            ?? throw new NotFoundException(nameof(CompletedActivity), activityId);

        await accessGuard.EnsureAthleteAccessAsync(activity.AthleteUserId, PermissionScope.ViewCompletedActivities, cancellationToken);
        return activity;
    }

    private static ActivityStreamsDto ToStreamsDto(Guid activityId, ExternalActivityStreams s) => new(
        activityId,
        s.TimeOffsetsSeconds,
        s.HeartRateBpm?.Select(RoundToInt).ToList(),
        s.CadenceRpm?.Select(RoundToInt).ToList(),
        s.WattsOutput?.Select(RoundToInt).ToList(),
        s.DistanceMeters?.Select(v => (decimal?)v).ToList(),
        s.AltitudeMeters?.Select(v => (decimal?)v).ToList(),
        s.VelocityMetersPerSecond?.Select(v => v is > 0 ? (int?)Math.Round(1000d / v.Value) : null).ToList(),
        s.GradePercent?.Select(v => (decimal?)v).ToList());

    private static int? RoundToInt(double? value) => value.HasValue ? (int?)Math.Round(value.Value) : null;

    private static CompletedActivityDto ToDto(CompletedActivity a) => new(
        a.Id, a.AthleteUserId, a.PlannedWorkoutId, a.Sport, a.Title, a.StartedAtUtc, a.DurationSeconds,
        a.DistanceMeters, a.ElevationGainMeters, a.AverageHeartRateBpm, a.MaxHeartRateBpm,
        a.AveragePaceSecondsPerKm, a.AveragePowerWatts, a.Calories, a.Provenance?.Source ?? DataSource.Manual);

    private static TrainingFeedbackDto ToFeedbackDto(TrainingFeedback f) => new(
        f.Id, f.AthleteUserId, f.PlannedWorkoutId, f.CompletedActivityId, f.Date, f.Rpe, f.LegsFeeling, f.OverallRating, f.PainNote, f.FreeText);
}
