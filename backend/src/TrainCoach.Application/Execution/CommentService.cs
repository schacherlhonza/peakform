using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Application.Execution;

public class CommentService(IApplicationDbContext db, IRelationshipAccessGuard accessGuard, IDateTimeProvider clock) : ICommentService
{
    public async Task<IReadOnlyList<CommentDto>> GetForWorkoutAsync(Guid plannedWorkoutId, CancellationToken cancellationToken = default)
    {
        var athleteUserId = await ResolveWorkoutAthleteAsync(plannedWorkoutId, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.CommentOnWorkouts, cancellationToken);
        return await ThreadAsync(db.Comments.Where(c => c.PlannedWorkoutId == plannedWorkoutId), cancellationToken);
    }

    /// <summary>Race threads use the same sharing as workout threads — coach and athlete talk about the race.</summary>
    public async Task<IReadOnlyList<CommentDto>> GetForRaceAsync(Guid raceId, CancellationToken cancellationToken = default)
    {
        var athleteUserId = await ResolveRaceAthleteAsync(raceId, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.CommentOnWorkouts, cancellationToken);
        return await ThreadAsync(db.Comments.Where(c => c.RaceId == raceId), cancellationToken);
    }

    public async Task<CommentDto> AddAsync(Guid callerUserId, CommentAuthorRole callerRole, CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var athleteUserId = request.RaceId is { } raceId
            ? await ResolveRaceAthleteAsync(raceId, cancellationToken)
            : await ResolveWorkoutAthleteAsync(request.PlannedWorkoutId!.Value, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.CommentOnWorkouts, cancellationToken);

        var comment = new Comment
        {
            PlannedWorkoutId = request.RaceId is null ? request.PlannedWorkoutId : null,
            RaceId = request.RaceId,
            AuthorUserId = callerUserId,
            AuthorRole = callerRole,
            Text = request.Text,
            CreatedAtUtc = clock.UtcNow,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        var name = await db.UserProfiles.Where(p => p.Id == callerUserId).Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(cancellationToken) ?? "?";
        return ToDto(comment, name);
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

    private async Task<IReadOnlyList<CommentDto>> ThreadAsync(IQueryable<Comment> query, CancellationToken cancellationToken)
    {
        var comments = await query.OrderBy(c => c.CreatedAtUtc).ToListAsync(cancellationToken);
        var authorIds = comments.Select(c => c.AuthorUserId).Distinct().ToList();
        var names = await db.UserProfiles.Where(p => authorIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        return comments.Select(c => ToDto(c, names.TryGetValue(c.AuthorUserId, out var profile) ? profile.DisplayName : "?")).ToList();
    }

    private static CommentDto ToDto(Comment c, string authorName) =>
        new(c.Id, c.PlannedWorkoutId, c.AuthorUserId, c.AuthorRole, authorName, c.Text, c.CreatedAtUtc, c.RaceId);

    private async Task<Guid> ResolveWorkoutAthleteAsync(Guid plannedWorkoutId, CancellationToken cancellationToken)
    {
        var athleteUserId = await db.PlannedWorkouts
            .Where(w => w.Id == plannedWorkoutId)
            .Join(db.TrainingWeeks, w => w.TrainingWeekId, tw => tw.Id, (w, tw) => tw.TrainingPlanId)
            .Join(db.TrainingPlans, planId => planId, p => p.Id, (planId, p) => (Guid?)p.AthleteUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return athleteUserId ?? throw new NotFoundException("PlannedWorkout", plannedWorkoutId);
    }

    private async Task<Guid> ResolveRaceAthleteAsync(Guid raceId, CancellationToken cancellationToken) =>
        await db.Races.Where(r => r.Id == raceId).Select(r => (Guid?)r.AthleteUserId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Race", raceId);
}
