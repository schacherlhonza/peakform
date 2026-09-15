using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Execution;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class CompletedActivityConfiguration : IEntityTypeConfiguration<CompletedActivity>
{
    public void Configure(EntityTypeBuilder<CompletedActivity> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.StartedAtUtc });
        builder.HasIndex(x => x.PlannedWorkoutId);

        builder.HasOne(x => x.Provenance).WithOne(x => x.CompletedActivity)
            .HasForeignKey<DataProvenance>(x => x.CompletedActivityId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AdditionalMetrics).WithOne(x => x.CompletedActivity)
            .HasForeignKey(x => x.CompletedActivityId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ActivityMetricConfiguration : IEntityTypeConfiguration<ActivityMetric>
{
    public void Configure(EntityTypeBuilder<ActivityMetric> builder)
    {
        builder.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.CompletedActivityId, x.MetricType, x.Source });
        // Mirrors CompletedActivity's soft-delete filter so a filtered-out activity doesn't
        // leave its metrics visible through direct queries (see EF Core warning on required
        // navigations with a filter on only one side).
        builder.HasQueryFilter(x => !x.CompletedActivity.IsDeleted);
    }
}

public class DataProvenanceConfiguration : IEntityTypeConfiguration<DataProvenance>
{
    public void Configure(EntityTypeBuilder<DataProvenance> builder)
    {
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        // Dedup key: same source + same external id must not be imported/synced twice.
        builder.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
        builder.HasQueryFilter(x => !x.CompletedActivity.IsDeleted);
    }
}

public class TrainingFeedbackConfiguration : IEntityTypeConfiguration<TrainingFeedback>
{
    public void Configure(EntityTypeBuilder<TrainingFeedback> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Date });
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.PlannedWorkoutId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
