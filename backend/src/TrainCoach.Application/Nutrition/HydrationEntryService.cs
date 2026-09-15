using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Nutrition;

namespace TrainCoach.Application.Nutrition;

/// <summary>Self-logged hydration data — same identity-check-on-write / guard-on-read split as FoodEntryService.</summary>
public class HydrationEntryService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IHydrationEntryService
{
    public async Task<IReadOnlyList<HydrationEntryDto>> GetForAthleteAsync(Guid athleteUserId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewNutrition, cancellationToken);

        var entries = await db.HydrationEntries
            .Where(e => e.AthleteUserId == athleteUserId && e.ConsumedAtUtc >= from && e.ConsumedAtUtc <= to)
            .OrderByDescending(e => e.ConsumedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<HydrationEntryDto> CreateAsync(Guid callerUserId, CreateHydrationEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Záznam o pitném režimu lze zapsat pouze pro sebe.");
        }

        var entry = new HydrationEntry
        {
            AthleteUserId = request.AthleteUserId,
            ConsumedAtUtc = request.ConsumedAtUtc,
            DrinkType = request.DrinkType,
            VolumeMilliliters = request.VolumeMilliliters,
            ContainsElectrolytes = request.ContainsElectrolytes,
            CaffeineMilligrams = request.CaffeineMilligrams,
            Note = request.Note,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };

        db.HydrationEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid callerUserId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await db.HydrationEntries.FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw new NotFoundException("HydrationEntry", entryId);

        if (callerUserId != entry.AthleteUserId)
        {
            throw new ForbiddenAccessException("Záznam o pitném režimu lze smazat pouze za sebe.");
        }

        db.HydrationEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static HydrationEntryDto ToDto(HydrationEntry e) => new(
        e.Id, e.AthleteUserId, e.ConsumedAtUtc, e.DrinkType, e.VolumeMilliliters, e.ContainsElectrolytes, e.CaffeineMilligrams, e.Note);
}
