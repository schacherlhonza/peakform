namespace TrainCoach.Application.Execution;

public interface ICommentService
{
    Task<IReadOnlyList<CommentDto>> GetForWorkoutAsync(Guid plannedWorkoutId, CancellationToken cancellationToken = default);
    Task<CommentDto> AddAsync(Guid callerUserId, Domain.Enums.CommentAuthorRole callerRole, CreateCommentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid callerUserId, Guid commentId, CancellationToken cancellationToken = default);
}
