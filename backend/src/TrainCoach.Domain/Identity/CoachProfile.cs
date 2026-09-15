using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Identity;

/// <summary>Shares its primary key with the owning <see cref="UserProfile"/> (1:1).</summary>
public class CoachProfile : AuditableEntity
{
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;

    public string? Bio { get; set; }
    public string? Certifications { get; set; }
    public int? YearsOfExperience { get; set; }
}
