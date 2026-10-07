using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

/// <summary>
/// Workout templates are coach-owned reference data, not athlete-scoped — a template isn't tied to
/// any one athlete, so ownership is a plain identity check (like <c>ActivityService.CreateManualAsync</c>)
/// rather than <see cref="IRelationshipAccessGuard"/>.
/// </summary>
public class WorkoutTemplateService(
    IApplicationDbContext db,
    IDateTimeProvider clock) : IWorkoutTemplateService
{
    public async Task<IReadOnlyList<WorkoutTemplateDto>> GetForCoachAsync(Guid coachUserId, CancellationToken cancellationToken = default)
    {
        var templates = await db.WorkoutTemplates
            .Include(t => t.Segments)
            .Where(t => t.CoachUserId == coachUserId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(ToDto).ToList();
    }

    public async Task<WorkoutTemplateDto> CreateAsync(Guid coachUserId, CreateWorkoutTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = new WorkoutTemplate
        {
            CoachUserId = coachUserId,
            Name = request.Name,
            Sport = request.Sport,
            Description = request.Description,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = coachUserId,
            Segments = WorkoutSegmentMapping.ToEntities(request.Segments),
        };
        db.WorkoutTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<WorkoutTemplateDto> UpdateAsync(Guid callerUserId, Guid id, UpdateWorkoutTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await db.WorkoutTemplates.Include(t => t.Segments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("WorkoutTemplate", id);

        if (template.CoachUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Šablonu tréninku může upravit pouze její autor.");
        }

        template.Name = request.Name;
        template.Sport = request.Sport;
        template.Description = request.Description;
        template.UpdatedAtUtc = clock.UtcNow;

        db.WorkoutSegments.RemoveRange(template.Segments);
        var newSegments = WorkoutSegmentMapping.ToEntities(request.Segments);
        template.Segments = newSegments;
        // See TrainingPlanService.UpdateWorkoutAsync for why this explicit AddRange is required.
        db.WorkoutSegments.AddRange(newSegments);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task DeleteAsync(Guid callerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var template = await db.WorkoutTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("WorkoutTemplate", id);

        if (template.CoachUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Šablonu tréninku může smazat pouze její autor.");
        }

        db.WorkoutTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static WorkoutTemplateDto ToDto(WorkoutTemplate t) => new(
        t.Id, t.CoachUserId, t.Name, t.Sport, t.Description,
        WorkoutSegmentMapping.ToDtos(t.Segments));
}
