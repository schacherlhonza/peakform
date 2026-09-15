using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class RecoveryMetricService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IRecoveryMetricService
{
    public async Task<IReadOnlyList<RecoveryMetricDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var records = await db.RecoveryMetrics
            .Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<RecoveryMetricDto> CreateOrUpdateAsync(Guid callerUserId, UpsertRecoveryMetricRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Regenerační metriku lze zapsat pouze pro sebe.");
        }

        var existing = await db.RecoveryMetrics.FirstOrDefaultAsync(
            r => r.AthleteUserId == request.AthleteUserId && r.Date == request.Date && r.Source == request.Source,
            cancellationToken);

        if (existing is null)
        {
            existing = new RecoveryMetric
            {
                AthleteUserId = request.AthleteUserId,
                Date = request.Date,
                Source = request.Source,
                CreatedAtUtc = clock.UtcNow,
                CreatedByUserId = callerUserId,
            };
            db.RecoveryMetrics.Add(existing);
        }

        existing.RestingHeartRateBpm = request.RestingHeartRateBpm;
        existing.ReadinessScore = request.ReadinessScore;
        existing.StressScore = request.StressScore;
        existing.UpdatedAtUtc = clock.UtcNow;
        existing.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    private static RecoveryMetricDto ToDto(RecoveryMetric r) => new(
        r.Id, r.AthleteUserId, r.Date, r.RestingHeartRateBpm, r.ReadinessScore, r.StressScore, r.Source);
}
