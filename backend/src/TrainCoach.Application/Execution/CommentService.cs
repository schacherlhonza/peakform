using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

public class CommentService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : ICommentService
{
    public async Task<IReadOnlyList<CommentDto>> GetForWorkoutAsync(Guid plannedWorkoutId, CancellationToken cancellationToken = default)
    {
        var athleteUserId = await ResolveAthleteAsync(plannedWorkoutId, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.CommentOnWorkouts, cancellationToken);

        var comments = await db.Comments
            .Where(c => c.PlannedWorkoutId == plannedWorkoutId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var authorIds = comments.Select(c => c.AuthorUserId).Distinct().ToList();
        var names = await db.UserProfiles.Where(p => authorIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return comments.Select(c => new CommentDto(
            c.Id, c.PlannedWorkoutId, c.AuthorUserId, c.AuthorRole,
            names.TryGetValue(c.AuthorUserId, out var profile) ? profile.DisplayName : "?",
            c.Text, c.CreatedAtUtc)).ToList();
    }

    public async Task<CommentDto> AddAsync(Guid callerUserId, CommentAuthorRole callerRole, CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var athleteUserId = await ResolveAthleteAsync(request.PlannedWorkoutId, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.CommentOnWorkouts, cancellationToken);

        var comment = new Comment
        {
            PlannedWorkoutId = request.PlannedWorkoutId,
            AuthorUserId = callerUserId,
            AuthorRole = callerRole,
            Text = request.Text,
            CreatedAtUtc = clock.UtcNow,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        var name = await db.UserProfiles.Where(p => p.Id == callerUserId).Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(cancellationToken) ?? "?";
        return new CommentDto(comment.Id, comment.PlannedWorkoutId, comment.AuthorUserId, comment.AuthorRole, name, comment.Text, comment.CreatedAtUtc);
    }

    public async Task DeleteAsync(Guid callerUserId, Guid commentId, CancellationToken cancellationToken = default)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Comment", commentId);

        if (comment.AuthorUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Komentář může smazat pouze jeho autor.");
        }

        comment.IsDeleted = true;
        comment.DeletedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> ResolveAthleteAsync(Guid plannedWorkoutId, CancellationToken cancellationToken)
    {
        var athleteUserId = await db.PlannedWorkouts
            .Where(w => w.Id == plannedWorkoutId)
            .Join(db.TrainingWeeks, w => w.TrainingWeekId, tw => tw.Id, (w, tw) => tw.TrainingPlanId)
            .Join(db.TrainingPlans, planId => planId, p => p.Id, (planId, p) => (Guid?)p.AthleteUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return athleteUserId ?? throw new NotFoundException("PlannedWorkout", plannedWorkoutId);
    }
}
