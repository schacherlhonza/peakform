using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Reporting;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class GeneratedReportConfiguration : IEntityTypeConfiguration<GeneratedReport>
{
    public void Configure(EntityTypeBuilder<GeneratedReport> builder)
    {
        builder.Property(x => x.NarrativeText).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.AthleteUserId, x.Date, x.Type }).IsUnique();

        builder.HasMany(x => x.Insights).WithOne(x => x.GeneratedReport)
            .HasForeignKey(x => x.GeneratedReportId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GeneratedReportInsightConfiguration : IEntityTypeConfiguration<GeneratedReportInsight>
{
    public void Configure(EntityTypeBuilder<GeneratedReportInsight> builder)
    {
        builder.Property(x => x.RuleCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500).IsRequired();
    }
}
