using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class HrvMeasurementService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IHrvMeasurementService
{
    public async Task<IReadOnlyList<HrvMeasurementDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var records = await db.HrvMeasurements
            .Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<HrvMeasurementDto> CreateOrUpdateAsync(Guid callerUserId, UpsertHrvMeasurementRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("HRV měření lze zapsat pouze pro sebe.");
        }

        var existing = await db.HrvMeasurements.FirstOrDefaultAsync(
            r => r.AthleteUserId == request.AthleteUserId && r.Date == request.Date && r.Source == request.Source,
            cancellationToken);

        if (existing is null)
        {
            existing = new HrvMeasurement
            {
                AthleteUserId = request.AthleteUserId,
                Date = request.Date,
                Source = request.Source,
                CreatedAtUtc = clock.UtcNow,
                CreatedByUserId = callerUserId,
            };
            db.HrvMeasurements.Add(existing);
        }

        existing.RmssdMs = request.RmssdMs;
        existing.MeasuredAtUtc = request.MeasuredAtUtc;
        existing.Notes = request.Notes;
        existing.UpdatedAtUtc = clock.UtcNow;
        existing.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    private static HrvMeasurementDto ToDto(HrvMeasurement r) => new(
        r.Id, r.AthleteUserId, r.Date, r.RmssdMs, r.Source, r.MeasuredAtUtc, r.Notes);
}
