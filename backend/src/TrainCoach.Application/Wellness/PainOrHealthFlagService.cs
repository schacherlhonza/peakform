using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

/// <summary>
/// Both the athlete and a coach with access may raise or update a health flag — this is
/// collaborative data, unlike the self-logged wellness entities below. There is no separate
/// "edit" scope for health flags, so ViewHealthFlags is deliberately reused to guard writes too.
/// </summary>
public class PainOrHealthFlagService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : IPainOrHealthFlagService
{
    public async Task<IReadOnlyList<PainOrHealthFlagDto>> GetForAthleteAsync(Guid athleteUserId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewHealthFlags, cancellationToken);

        var query = db.PainOrHealthFlags.Where(f => f.AthleteUserId == athleteUserId);
        if (activeOnly)
        {
            query = query.Where(f => f.Status != HealthFlagStatus.Resolved);
        }

        var flags = await query.OrderByDescending(f => f.StartedOnDate).ToListAsync(cancellationToken);
        return flags.Select(ToDto).ToList();
    }

    public async Task<PainOrHealthFlagDto> CreateAsync(Guid callerUserId, CreatePainOrHealthFlagRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.ViewHealthFlags, cancellationToken);

        var flag = new PainOrHealthFlag
        {
            AthleteUserId = request.AthleteUserId,
            RelatedCheckInId = request.RelatedCheckInId,
            Type = request.Type,
            Severity = request.Severity,
            Status = HealthFlagStatus.Active,
            BodyPart = request.BodyPart,
            Description = request.Description,
            StartedOnDate = request.StartedOnDate,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = callerUserId,
        };

        db.PainOrHealthFlags.Add(flag);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(flag);
    }

    public async Task<PainOrHealthFlagDto> UpdateStatusAsync(Guid callerUserId, Guid flagId, UpdateHealthFlagStatusRequest request, CancellationToken cancellationToken = default)
    {
        var flag = await db.PainOrHealthFlags.FirstOrDefaultAsync(f => f.Id == flagId, cancellationToken)
            ?? throw new NotFoundException("PainOrHealthFlag", flagId);

        await accessGuard.EnsureAthleteAccessAsync(flag.AthleteUserId, PermissionScope.ViewHealthFlags, cancellationToken);

        flag.Status = request.Status;
        flag.ResolvedOnDate = request.Status == HealthFlagStatus.Resolved ? clock.TodayUtc : null;
        flag.UpdatedAtUtc = clock.UtcNow;
        flag.UpdatedByUserId = callerUserId;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(flag);
    }

    private static PainOrHealthFlagDto ToDto(PainOrHealthFlag f) => new(
        f.Id, f.AthleteUserId, f.RelatedCheckInId, f.Type, f.Severity, f.Status, f.BodyPart, f.Description, f.StartedOnDate, f.ResolvedOnDate);
}
