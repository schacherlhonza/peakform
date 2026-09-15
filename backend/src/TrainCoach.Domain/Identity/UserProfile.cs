using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Identity;

/// <summary>
/// Domain-facing profile data for a user. Shares its primary key with the Identity user
/// record (owned by the Infrastructure layer) in a 1:1 relationship — Identity/auth concerns
/// stay in Infrastructure, display/domain data lives here.
/// </summary>
public class UserProfile : AuditableEntity, ISoftDeletable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName => $"{FirstName} {LastName}".Trim();
    public AppRole PrimaryRole { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Prague";
    public string Locale { get; set; } = "cs-CZ";
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsOnboarded { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public AthleteProfile? AthleteProfile { get; set; }
    public CoachProfile? CoachProfile { get; set; }
}
