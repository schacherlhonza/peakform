using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Execution;

public record CommentDto(Guid Id, Guid PlannedWorkoutId, Guid AuthorUserId, CommentAuthorRole AuthorRole, string AuthorName, string Text, DateTime CreatedAtUtc);

public record CreateCommentRequest(Guid PlannedWorkoutId, string Text);
