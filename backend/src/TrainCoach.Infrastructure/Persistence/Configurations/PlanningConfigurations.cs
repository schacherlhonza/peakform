using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.AthleteUserId);
    }
}

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.AthleteUserId);
    }
}

public class RaceConfiguration : IEntityTypeConfiguration<Race>
{
    public void Configure(EntityTypeBuilder<Race> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.AthleteUserId, x.StartsAtUtc });
    }
}

public class TrainingPlanConfiguration : IEntityTypeConfiguration<TrainingPlan>
{
    public void Configure(EntityTypeBuilder<TrainingPlan> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.AthleteUserId);
        builder.HasIndex(x => x.CoachUserId);
        builder.HasMany(x => x.Weeks).WithOne(x => x.TrainingPlan)
            .HasForeignKey(x => x.TrainingPlanId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TrainingWeekConfiguration : IEntityTypeConfiguration<TrainingWeek>
{
    public void Configure(EntityTypeBuilder<TrainingWeek> builder)
    {
        builder.HasIndex(x => new { x.TrainingPlanId, x.WeekStartDate }).IsUnique();
        builder.HasMany(x => x.Workouts).WithOne(x => x.TrainingWeek)
            .HasForeignKey(x => x.TrainingWeekId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.TrainingPlan.IsDeleted);
    }
}

public class PlannedWorkoutConfiguration : IEntityTypeConfiguration<PlannedWorkout>
{
    public void Configure(EntityTypeBuilder<PlannedWorkout> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Date);
        builder.HasMany(x => x.Segments).WithOne(x => x.PlannedWorkout)
            .HasForeignKey(x => x.PlannedWorkoutId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class WorkoutTemplateConfiguration : IEntityTypeConfiguration<WorkoutTemplate>
{
    public void Configure(EntityTypeBuilder<WorkoutTemplate> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.CoachUserId);
        builder.HasMany(x => x.Segments).WithOne(x => x.WorkoutTemplate)
            .HasForeignKey(x => x.WorkoutTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkoutSegmentConfiguration : IEntityTypeConfiguration<WorkoutSegment>
{
    public void Configure(EntityTypeBuilder<WorkoutSegment> builder)
    {
        builder.HasIndex(x => new { x.PlannedWorkoutId, x.Order });
        builder.HasIndex(x => new { x.WorkoutTemplateId, x.Order });
    }
}

public class HeartRateZoneConfiguration : IEntityTypeConfiguration<HeartRateZone>
{
    public void Configure(EntityTypeBuilder<HeartRateZone> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.AthleteUserId, x.ZoneNumber, x.EffectiveFromDate });
    }
}

public class CustomAbbreviationConfiguration : IEntityTypeConfiguration<CustomAbbreviation>
{
    public void Configure(EntityTypeBuilder<CustomAbbreviation> builder)
    {
        builder.Property(x => x.Abbreviation).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FullText).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.CoachUserId, x.Abbreviation }).IsUnique();
    }
}
