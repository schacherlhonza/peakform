namespace TrainCoach.Domain.Common;

/// <summary>
/// Base type for every entity. All timestamps are stored and compared in UTC;
/// the presentation layer converts to the user's time zone.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedByUserId { get; set; }
}
