using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

public class RaceService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock) : IRaceService
{
    public async Task<IReadOnlyList<RaceDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        var races = await db.Races
            .Where(r => r.AthleteUserId == athleteUserId)
            .OrderBy(r => r.StartsAtUtc)
            .ToListAsync(cancellationToken);

        return races.Select(ToDto).ToList();
    }

    public async Task<RaceDto> CreateAsync(Guid callerUserId, CreateRaceRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var race = new Race
        {
            AthleteUserId = request.AthleteUserId,
            SeasonId = request.SeasonId,
            GoalId = request.GoalId,
            Name = request.Name,
            Sport = request.Sport,
            StartsAtUtc = request.StartsAtUtc,
            Location = request.Location,
            DistanceMeters = request.DistanceMeters,
            ElevationGainMeters = request.ElevationGainMeters,
            Priority = request.Priority,
            TargetTimeSeconds = request.TargetTimeSeconds,
            TargetResultNote = request.TargetResultNote,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };
        db.Races.Add(race);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(race);
    }

    public async Task<RaceDto> UpdateAsync(Guid id, UpdateRaceRequest request, CancellationToken cancellationToken = default)
    {
        var race = await db.Races.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Race", id);

        await accessGuard.EnsureAthleteAccessAsync(race.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        race.SeasonId = request.SeasonId;
        race.GoalId = request.GoalId;
        race.Name = request.Name;
        race.Sport = request.Sport;
        race.StartsAtUtc = request.StartsAtUtc;
        race.Location = request.Location;
        race.DistanceMeters = request.DistanceMeters;
        race.ElevationGainMeters = request.ElevationGainMeters;
        race.Priority = request.Priority;
        race.TargetTimeSeconds = request.TargetTimeSeconds;
        race.TargetResultNote = request.TargetResultNote;
        race.ActualTimeSeconds = request.ActualTimeSeconds;
        race.ActualResultNote = request.ActualResultNote;
        race.ResultNotes = request.ResultNotes;
        race.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(race);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var race = await db.Races.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Race", id);

        await accessGuard.EnsureAthleteAccessAsync(race.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        db.Races.Remove(race);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static RaceDto ToDto(Race r) => new(
        r.Id, r.AthleteUserId, r.SeasonId, r.GoalId, r.Name, r.Sport, r.StartsAtUtc, r.Location,
        r.DistanceMeters, r.ElevationGainMeters, r.Priority, r.TargetTimeSeconds, r.TargetResultNote,
        r.ActualTimeSeconds, r.ActualResultNote, r.ResultNotes);
}
