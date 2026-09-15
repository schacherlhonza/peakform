using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

/// <summary>
/// Both the athlete and a coach with access may record a personal record — collaborative data
/// like PainOrHealthFlag, so ViewPersonalRecords is reused to guard both reads and writes.
/// </summary>
public class PersonalRecordService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IPersonalRecordService
{
    public async Task<IReadOnlyList<PersonalRecordDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewPersonalRecords, cancellationToken);

        var records = await db.PersonalRecords
            .Where(r => r.AthleteUserId == athleteUserId)
            .OrderByDescending(r => r.AchievedDate)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task<PersonalRecordDto> CreateAsync(Guid callerUserId, CreatePersonalRecordRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.ViewPersonalRecords, cancellationToken);

        var record = new PersonalRecord
        {
            AthleteUserId = request.AthleteUserId,
            Sport = request.Sport,
            DistanceLabel = request.DistanceLabel,
            TimeSeconds = request.TimeSeconds,
            AchievedDate = request.AchievedDate,
            RaceId = request.RaceId,
            Notes = request.Notes,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };

        db.PersonalRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(record);
    }

    public async Task<PersonalRecordDto> UpdateAsync(Guid callerUserId, Guid recordId, UpdatePersonalRecordRequest request, CancellationToken cancellationToken = default)
    {
        var record = await db.PersonalRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken)
            ?? throw new NotFoundException("PersonalRecord", recordId);

        await accessGuard.EnsureAthleteAccessAsync(record.AthleteUserId, PermissionScope.ViewPersonalRecords, cancellationToken);

        record.Sport = request.Sport;
        record.DistanceLabel = request.DistanceLabel;
        record.TimeSeconds = request.TimeSeconds;
        record.AchievedDate = request.AchievedDate;
        record.RaceId = request.RaceId;
        record.Notes = request.Notes;
        record.UpdatedAtUtc = clock.UtcNow;
        record.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(record);
    }

    public async Task DeleteAsync(Guid callerUserId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await db.PersonalRecords.FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken)
            ?? throw new NotFoundException("PersonalRecord", recordId);

        await accessGuard.EnsureAthleteAccessAsync(record.AthleteUserId, PermissionScope.ViewPersonalRecords, cancellationToken);

        db.PersonalRecords.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static PersonalRecordDto ToDto(PersonalRecord r) => new(
        r.Id, r.AthleteUserId, r.Sport, r.DistanceLabel, r.TimeSeconds, r.AchievedDate, r.RaceId, r.Notes);
}
