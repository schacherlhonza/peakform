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
        builder.HasIndex(x => x.NormalizedFingerprint);
        // Plain (non-FK) index — see the property's doc comment for why this isn't a real FK.
        builder.HasIndex(x => x.PrimarySourceRecordId);

        builder.HasMany(x => x.SourceRecords).WithOne(x => x.CompletedActivity)
            .HasForeignKey(x => x.CompletedActivityId).OnDelete(DeleteBehavior.Cascade);

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

public class ActivitySourceRecordConfiguration : IEntityTypeConfiguration<ActivitySourceRecord>
{
    public void Configure(EntityTypeBuilder<ActivitySourceRecord> builder)
    {
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        // Level-1 dedup key: same source + same external id must not be imported/synced twice.
        // This is the one check that must never be replaced by fuzzy/fingerprint matching.
        builder.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
        builder.HasIndex(x => x.NormalizedFingerprint);
        builder.HasIndex(x => x.StravaArchiveImportId);
        builder.HasQueryFilter(x => !x.CompletedActivity.IsDeleted);
    }
}

public class ActivityStreamConfiguration : IEntityTypeConfiguration<ActivityStream>
{
    public void Configure(EntityTypeBuilder<ActivityStream> builder)
    {
        builder.HasOne(x => x.ActivitySourceRecord).WithOne(x => x.Stream)
            .HasForeignKey<ActivityStream>(x => x.ActivitySourceRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ActivitySourceRecordId).IsUnique();
        builder.Property(x => x.Payload).IsRequired();
        builder.HasQueryFilter(x => !x.ActivitySourceRecord.CompletedActivity.IsDeleted);
    }
}

public class ActivityBestEffortConfiguration : IEntityTypeConfiguration<ActivityBestEffort>
{
    public void Configure(EntityTypeBuilder<ActivityBestEffort> builder)
    {
        builder.HasOne(x => x.CompletedActivity).WithMany(x => x.BestEfforts)
            .HasForeignKey(x => x.CompletedActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CompletedActivityId, x.Type }).IsUnique();
        builder.HasIndex(x => new { x.AthleteUserId, x.Sport, x.Type });
        builder.Property(x => x.Value).HasPrecision(10, 2);
        builder.HasQueryFilter(x => !x.CompletedActivity.IsDeleted);
    }
}

public class MergeDecisionConfiguration : IEntityTypeConfiguration<MergeDecision>
{
    public void Configure(EntityTypeBuilder<MergeDecision> builder)
    {
        builder.HasIndex(x => x.SurvivingActivityId);
        builder.HasIndex(x => x.AthleteUserId);
        builder.HasIndex(x => x.Outcome);
    }
}

public class DuplicateCandidateConfiguration : IEntityTypeConfiguration<DuplicateCandidate>
{
    public void Configure(EntityTypeBuilder<DuplicateCandidate> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Status });
    }
}

public class DuplicateDryRunReportConfiguration : IEntityTypeConfiguration<DuplicateDryRunReport>
{
    public void Configure(EntityTypeBuilder<DuplicateDryRunReport> builder)
    {
        builder.HasIndex(x => x.GeneratedAtUtc);
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
