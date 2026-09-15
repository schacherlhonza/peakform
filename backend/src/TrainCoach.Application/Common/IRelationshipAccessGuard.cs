using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Common;

/// <summary>
/// The single place that decides whether the current caller may touch a given athlete's data.
/// Every service that reads/writes athlete-scoped data (plans, activities, check-ins, nutrition,
/// reports, ...) must call this before returning anything — a coach must never reach an
/// athlete's data by ID alone, only through an Active relationship with the right granted scope.
/// </summary>
public interface IRelationshipAccessGuard
{
    /// <summary>Throws <see cref="ForbiddenAccessException"/> unless the caller IS the athlete,
    /// or is a coach with an Active relationship to that athlete granting <paramref name="requiredScope"/>
    /// (when specified — pass null for "any active relationship, no specific scope required").</summary>
    Task EnsureAthleteAccessAsync(Guid targetAthleteUserId, PermissionScope? requiredScope, CancellationToken cancellationToken = default);

    /// <summary>Non-throwing variant, for building filtered lists (e.g. a coach's roster) rather than guarding a single resource.</summary>
    Task<bool> HasAthleteAccessAsync(Guid targetAthleteUserId, PermissionScope? requiredScope, CancellationToken cancellationToken = default);
}
