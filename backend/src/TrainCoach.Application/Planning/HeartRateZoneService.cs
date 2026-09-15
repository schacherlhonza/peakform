using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

public class HeartRateZoneService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock) : IHeartRateZoneService
{
    public async Task<IReadOnlyList<HeartRateZoneDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        var zones = await db.HeartRateZones
            .Where(z => z.AthleteUserId == athleteUserId)
            .OrderByDescending(z => z.EffectiveFromDate).ThenBy(z => z.ZoneNumber)
            .ToListAsync(cancellationToken);

        return zones.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<HeartRateZoneDto>> SetZonesAsync(Guid callerUserId, SetHeartRateZonesRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var existing = await db.HeartRateZones
            .Where(z => z.AthleteUserId == request.AthleteUserId && z.EffectiveFromDate == request.EffectiveFromDate)
            .ToListAsync(cancellationToken);
        db.HeartRateZones.RemoveRange(existing);

        var zones = request.Zones.Select(z => new HeartRateZone
        {
            AthleteUserId = request.AthleteUserId,
            ZoneNumber = z.ZoneNumber,
            Name = z.Name,
            MinBpm = z.MinBpm,
            MaxBpm = z.MaxBpm,
            MinPaceSecondsPerKm = z.MinPaceSecondsPerKm,
            MaxPaceSecondsPerKm = z.MaxPaceSecondsPerKm,
            EffectiveFromDate = request.EffectiveFromDate,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        }).ToList();

        db.HeartRateZones.AddRange(zones);
        await db.SaveChangesAsync(cancellationToken);

        return zones.OrderBy(z => z.ZoneNumber).Select(ToDto).ToList();
    }

    private static HeartRateZoneDto ToDto(HeartRateZone z) => new(
        z.Id, z.AthleteUserId, z.ZoneNumber, z.Name, z.MinBpm, z.MaxBpm, z.MinPaceSecondsPerKm, z.MaxPaceSecondsPerKm, z.EffectiveFromDate);
}
