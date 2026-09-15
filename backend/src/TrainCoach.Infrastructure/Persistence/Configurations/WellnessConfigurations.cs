using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class DailyCheckInConfiguration : IEntityTypeConfiguration<DailyCheckIn>
{
    public void Configure(EntityTypeBuilder<DailyCheckIn> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Date, x.Type }).IsUnique();
    }
}

public class PainOrHealthFlagConfiguration : IEntityTypeConfiguration<PainOrHealthFlag>
{
    public void Configure(EntityTypeBuilder<PainOrHealthFlag> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Status });
    }
}

public class SleepRecordConfiguration : IEntityTypeConfiguration<SleepRecord>
{
    public void Configure(EntityTypeBuilder<SleepRecord> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Date, x.Source }).IsUnique();
    }
}

public class RecoveryMetricConfiguration : IEntityTypeConfiguration<RecoveryMetric>
{
    public void Configure(EntityTypeBuilder<RecoveryMetric> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Date, x.Source }).IsUnique();
    }
}

public class HrvMeasurementConfiguration : IEntityTypeConfiguration<HrvMeasurement>
{
    public void Configure(EntityTypeBuilder<HrvMeasurement> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Date, x.Source }).IsUnique();
    }
}

public class PerformanceBaselineConfiguration : IEntityTypeConfiguration<PerformanceBaseline>
{
    public void Configure(EntityTypeBuilder<PerformanceBaseline> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.MetricType, x.ValidFromDate });
    }
}

public class PersonalRecordConfiguration : IEntityTypeConfiguration<PersonalRecord>
{
    public void Configure(EntityTypeBuilder<PersonalRecord> builder)
    {
        builder.Property(x => x.DistanceLabel).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.AthleteUserId, x.Sport });
    }
}
