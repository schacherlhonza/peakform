using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Nutrition;

namespace TrainCoach.Application.Nutrition;

/// <summary>
/// Self-logged nutrition data — only the athlete may write it (plain identity check), but a
/// coach with the right permission may read it, same split as SleepRecordService.
/// </summary>
public class FoodEntryService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IFoodEntryService
{
    public async Task<IReadOnlyList<FoodEntryDto>> GetForAthleteAsync(Guid athleteUserId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewNutrition, cancellationToken);

        var entries = await db.FoodEntries
            .Where(e => e.AthleteUserId == athleteUserId && e.ConsumedAtUtc >= from && e.ConsumedAtUtc <= to)
            .OrderByDescending(e => e.ConsumedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<FoodEntryDto> CreateAsync(Guid callerUserId, CreateFoodEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Záznam o jídle lze zapsat pouze pro sebe.");
        }

        var entry = new FoodEntry
        {
            AthleteUserId = request.AthleteUserId,
            ConsumedAtUtc = request.ConsumedAtUtc,
            MealType = request.MealType,
            Description = request.Description,
            EstimatedCarbsGrams = request.EstimatedCarbsGrams,
            EstimatedProteinGrams = request.EstimatedProteinGrams,
            PhotoUrl = request.PhotoUrl,
            RelativeToWorkoutNote = request.RelativeToWorkoutNote,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };

        db.FoodEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid callerUserId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await db.FoodEntries.FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw new NotFoundException("FoodEntry", entryId);

        if (callerUserId != entry.AthleteUserId)
        {
            throw new ForbiddenAccessException("Záznam o jídle lze smazat pouze za sebe.");
        }

        db.FoodEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static FoodEntryDto ToDto(FoodEntry e) => new(
        e.Id, e.AthleteUserId, e.ConsumedAtUtc, e.MealType, e.Description,
        e.EstimatedCarbsGrams, e.EstimatedProteinGrams, e.PhotoUrl, e.RelativeToWorkoutNote);
}
