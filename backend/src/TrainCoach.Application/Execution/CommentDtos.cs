using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Execution;

public record CommentDto(Guid Id, Guid? PlannedWorkoutId, Guid AuthorUserId, CommentAuthorRole AuthorRole, string AuthorName, string Text, DateTime CreatedAtUtc, Guid? RaceId = null);

/// <summary>Exactly one of <paramref name="PlannedWorkoutId"/> and <paramref name="RaceId"/>.</summary>
public record CreateCommentRequest(Guid? PlannedWorkoutId, string Text, Guid? RaceId = null);
