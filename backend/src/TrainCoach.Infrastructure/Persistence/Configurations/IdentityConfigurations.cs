using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Identity;
using TrainCoach.Infrastructure.Identity;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(16).IsRequired();
        builder.Ignore(x => x.DisplayName);

        // Shares its PK with the Identity user (1:1, no navigation on the Identity side).
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserProfile>(x => x.Id).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AthleteProfileConfiguration : IEntityTypeConfiguration<AthleteProfile>
{
    public void Configure(EntityTypeBuilder<AthleteProfile> builder)
    {
        builder.HasIndex(x => x.UserProfileId).IsUnique();
        builder.HasOne(x => x.UserProfile).WithOne(x => x.AthleteProfile)
            .HasForeignKey<AthleteProfile>(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.UserProfile.IsDeleted);
    }
}

public class CoachProfileConfiguration : IEntityTypeConfiguration<CoachProfile>
{
    public void Configure(EntityTypeBuilder<CoachProfile> builder)
    {
        builder.HasIndex(x => x.UserProfileId).IsUnique();
        builder.HasOne(x => x.UserProfile).WithOne(x => x.CoachProfile)
            .HasForeignKey<CoachProfile>(x => x.UserProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.UserProfile.IsDeleted);
    }
}

public class CoachAthleteRelationshipConfiguration : IEntityTypeConfiguration<CoachAthleteRelationship>
{
    public void Configure(EntityTypeBuilder<CoachAthleteRelationship> builder)
    {
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CoachUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AthleteUserId).OnDelete(DeleteBehavior.Restrict);

        // A coach and athlete may have at most one relationship row per status lifecycle at a
        // time in practice, but re-invites after Ended/Revoked/Declined are legitimate, so we
        // only index for lookup speed rather than a hard uniqueness constraint.
        builder.HasIndex(x => new { x.CoachUserId, x.AthleteUserId });
        builder.HasIndex(x => new { x.AthleteUserId, x.Status });

        builder.HasMany(x => x.Permissions).WithOne(x => x.Relationship)
            .HasForeignKey(x => x.RelationshipId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RelationshipPermissionConfiguration : IEntityTypeConfiguration<RelationshipPermission>
{
    public void Configure(EntityTypeBuilder<RelationshipPermission> builder)
    {
        builder.HasIndex(x => new { x.RelationshipId, x.Scope });
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.EntityName).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}
