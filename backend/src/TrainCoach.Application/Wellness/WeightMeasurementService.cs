using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class WeightMeasurementService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IWeightMeasurementService
{
    public async Task<IReadOnlyList<WeightMeasurementDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var records = await db.WeightMeasurements
            .Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<WeightMeasurementDto> CreateOrUpdateAsync(Guid callerUserId, UpsertWeightMeasurementRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Váhu lze zapsat pouze pro sebe.");
        }

        var existing = await db.WeightMeasurements.FirstOrDefaultAsync(
            r => r.AthleteUserId == request.AthleteUserId && r.Date == request.Date && r.Source == request.Source,
            cancellationToken);

        if (existing is null)
        {
            existing = new WeightMeasurement
            {
                AthleteUserId = request.AthleteUserId,
                Date = request.Date,
                Source = request.Source,
                CreatedAtUtc = clock.UtcNow,
                CreatedByUserId = callerUserId,
            };
            db.WeightMeasurements.Add(existing);
        }

        existing.WeightKg = request.WeightKg;
        existing.UpdatedAtUtc = clock.UtcNow;
        existing.UpdatedByUserId = callerUserId;

        // Keep the single-value profile field in sync with whichever measurement is most recent
        // across all sources — same rule SyncOrchestrator applies for provider-synced weight.
        var latestDate = await db.WeightMeasurements
            .Where(w => w.AthleteUserId == request.AthleteUserId)
            .OrderByDescending(w => w.Date)
            .Select(w => (DateOnly?)w.Date)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestDate is null || latestDate <= request.Date)
        {
            var profile = await db.AthleteProfiles.FirstOrDefaultAsync(p => p.UserProfileId == request.AthleteUserId, cancellationToken);
            if (profile is not null)
            {
                profile.CurrentWeightKg = request.WeightKg;
                profile.UpdatedAtUtc = clock.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    private static WeightMeasurementDto ToDto(WeightMeasurement r) => new(r.Id, r.AthleteUserId, r.Date, r.WeightKg, r.Source);
}
