using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

/// <summary>
/// Self-logged / wearable-derived data the athlete owns — only the athlete may write it, but a
/// coach with the right permission may read it (see ActivityService.CreateManualAsync for the
/// same identity-check-on-write / guard-on-read split).
/// </summary>
public class SleepRecordService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : ISleepRecordService
{
    public async Task<IReadOnlyList<SleepRecordDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var records = await db.SleepRecords
            .Where(r => r.AthleteUserId == athleteUserId && r.Date >= from && r.Date <= to)
            .OrderByDescending(r => r.Date)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<SleepRecordDto> CreateOrUpdateAsync(Guid callerUserId, UpsertSleepRecordRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Záznam o spánku lze zapsat pouze pro sebe.");
        }

        var existing = await db.SleepRecords.FirstOrDefaultAsync(
            r => r.AthleteUserId == request.AthleteUserId && r.Date == request.Date && r.Source == request.Source,
            cancellationToken);

        if (existing is null)
        {
            existing = new SleepRecord
            {
                AthleteUserId = request.AthleteUserId,
                Date = request.Date,
                Source = request.Source,
                CreatedAtUtc = clock.UtcNow,
                CreatedByUserId = callerUserId,
            };
            db.SleepRecords.Add(existing);
        }

        existing.DurationMinutes = request.DurationMinutes;
        existing.DeepSleepMinutes = request.DeepSleepMinutes;
        existing.RemSleepMinutes = request.RemSleepMinutes;
        existing.Notes = request.Notes;
        existing.UpdatedAtUtc = clock.UtcNow;
        existing.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    private static SleepRecordDto ToDto(SleepRecord r) => new(
        r.Id, r.AthleteUserId, r.Date, r.DurationMinutes, r.DeepSleepMinutes, r.RemSleepMinutes, r.Source, r.Notes);
}
