using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Identity;

/// <summary>Shares its primary key with the owning <see cref="UserProfile"/> (1:1).</summary>
public class AthleteProfile : AuditableEntity
{
    public Guid UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? CurrentWeightKg { get; set; }
    public int? RestingHeartRateBpm { get; set; }
    public int? MaxHeartRateBpm { get; set; }
    public string? PrimarySport { get; set; }
    public string? Notes { get; set; }
}
