using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class TrainingLoadService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard) : ITrainingLoadService
{
    public async Task<IReadOnlyList<TrainingLoadSnapshotDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var records = await db.TrainingLoadSnapshots
            .Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    private static TrainingLoadSnapshotDto ToDto(TrainingLoadSnapshot r) => new(r.Id, r.AthleteUserId, r.Date, r.Ctl, r.Atl, r.RampRate, r.Source);
}
