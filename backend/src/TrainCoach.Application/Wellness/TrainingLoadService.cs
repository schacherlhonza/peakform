using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class TrainingLoadService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard) : ITrainingLoadService
{
    public async Task<IReadOnlyList<TrainingLoadSnapshotDto>> GetForAthleteAsync(
        Guid athleteUserId, DateOnly from, DateOnly to, DataSource? source = null, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var query = db.TrainingLoadSnapshots.Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= to);
        if (source is { } only)
        {
            query = query.Where(r => r.Source == only);
        }
        var records = await query.ToListAsync(cancellationToken);

        // Several sources can have the same day (intervals.icu and PeakForm): keep the
        // highest-precedence one — values from different sources are never mixed in one day.
        return records
            .GroupBy(r => r.Date)
            // One snapshot carries CTL, ATL and ramp rate together; CTL's precedence decides for all.
            .Select(g => g.OrderBy(r => DailyMetricSelectionService.DefaultRankFor(WellnessMetricKind.Ctl, r.Source)).First())
            .OrderByDescending(r => r.Date)
            .Select(ToDto)
            .ToList();
    }

    private static TrainingLoadSnapshotDto ToDto(TrainingLoadSnapshot r) => new(r.Id, r.AthleteUserId, r.Date, r.Ctl, r.Atl, r.RampRate, r.Source);
}
