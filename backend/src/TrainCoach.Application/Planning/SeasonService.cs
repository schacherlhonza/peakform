using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

public class SeasonService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock) : ISeasonService
{
    public async Task<IReadOnlyList<SeasonDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        var seasons = await db.Seasons
            .Where(s => s.AthleteUserId == athleteUserId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);

        return seasons.Select(ToDto).ToList();
    }

    public async Task<SeasonDto> CreateAsync(Guid callerUserId, CreateSeasonRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var season = new Season
        {
            AthleteUserId = request.AthleteUserId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Notes = request.Notes,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(season);
    }

    public async Task<SeasonDto> UpdateAsync(Guid id, UpdateSeasonRequest request, CancellationToken cancellationToken = default)
    {
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Season", id);

        await accessGuard.EnsureAthleteAccessAsync(season.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        season.Name = request.Name;
        season.StartDate = request.StartDate;
        season.EndDate = request.EndDate;
        season.Notes = request.Notes;
        season.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(season);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Season", id);

        await accessGuard.EnsureAthleteAccessAsync(season.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        db.Seasons.Remove(season);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static SeasonDto ToDto(Season s) => new(s.Id, s.AthleteUserId, s.Name, s.StartDate, s.EndDate, s.Notes);
}
