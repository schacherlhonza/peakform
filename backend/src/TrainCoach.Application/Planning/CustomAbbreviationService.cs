using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

/// <summary>
/// A coach's abbreviation dictionary is pure reference data, not athlete-scoped — ownership is a
/// plain identity check (like <see cref="WorkoutTemplateService"/>), not <see cref="IRelationshipAccessGuard"/>.
/// </summary>
public class CustomAbbreviationService(
    IApplicationDbContext db,
    IDateTimeProvider clock) : ICustomAbbreviationService
{
    public async Task<IReadOnlyList<CustomAbbreviationDto>> GetForCoachAsync(Guid coachUserId, CancellationToken cancellationToken = default)
    {
        var abbreviations = await db.CustomAbbreviations
            .Where(a => a.CoachUserId == coachUserId)
            .OrderBy(a => a.Abbreviation)
            .ToListAsync(cancellationToken);

        return abbreviations.Select(ToDto).ToList();
    }

    public async Task<CustomAbbreviationDto> CreateAsync(Guid coachUserId, CreateCustomAbbreviationRequest request, CancellationToken cancellationToken = default)
    {
        var abbreviation = new CustomAbbreviation
        {
            CoachUserId = coachUserId,
            Abbreviation = request.Abbreviation,
            FullText = request.FullText,
            Description = request.Description,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = coachUserId,
        };
        db.CustomAbbreviations.Add(abbreviation);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(abbreviation);
    }

    public async Task<CustomAbbreviationDto> UpdateAsync(Guid callerUserId, Guid id, UpdateCustomAbbreviationRequest request, CancellationToken cancellationToken = default)
    {
        var abbreviation = await db.CustomAbbreviations.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("CustomAbbreviation", id);

        if (abbreviation.CoachUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Zkratku může upravit pouze její autor.");
        }

        abbreviation.Abbreviation = request.Abbreviation;
        abbreviation.FullText = request.FullText;
        abbreviation.Description = request.Description;
        abbreviation.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(abbreviation);
    }

    public async Task DeleteAsync(Guid callerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var abbreviation = await db.CustomAbbreviations.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException("CustomAbbreviation", id);

        if (abbreviation.CoachUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Zkratku může smazat pouze její autor.");
        }

        db.CustomAbbreviations.Remove(abbreviation);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CustomAbbreviationDto ToDto(CustomAbbreviation a) => new(
        a.Id, a.CoachUserId, a.Abbreviation, a.FullText, a.Description);
}
