using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>A coach/athlete comment thread attached to a specific planned workout day.</summary>
public class Comment : AuditableEntity, ISoftDeletable
{
    public Guid PlannedWorkoutId { get; set; }
    public Guid AuthorUserId { get; set; }
    public CommentAuthorRole AuthorRole { get; set; }
    public string Text { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
