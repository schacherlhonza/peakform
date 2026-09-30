using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Integrations;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Provider }).IsUnique();

        builder.HasOne(x => x.Credential).WithOne(x => x.IntegrationConnection)
            .HasForeignKey<IntegrationCredential>(x => x.IntegrationConnectionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.SyncRuns).WithOne(x => x.IntegrationConnection)
            .HasForeignKey(x => x.IntegrationConnectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SynchronizationRunConfiguration : IEntityTypeConfiguration<SynchronizationRun>
{
    public void Configure(EntityTypeBuilder<SynchronizationRun> builder)
    {
        builder.HasIndex(x => new { x.IntegrationConnectionId, x.StartedAtUtc });
    }
}

public class ImportedFileConfiguration : IEntityTypeConfiguration<ImportedFile>
{
    public void Configure(EntityTypeBuilder<ImportedFile> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.AthleteUserId, x.ContentHash });
    }
}

public class StravaArchiveImportConfiguration : IEntityTypeConfiguration<StravaArchiveImport>
{
    public void Configure(EntityTypeBuilder<StravaArchiveImport> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(260);
        builder.Property(x => x.StravaAthleteId).HasMaxLength(40);
        builder.Property(x => x.StorageKey).HasMaxLength(100);
        builder.Property(x => x.SportsFilter).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => new { x.AthleteUserId, x.CreatedAtUtc });
        builder.HasIndex(x => x.Status);
    }
}

public class ConnectorDomainPolicyConfiguration : IEntityTypeConfiguration<ConnectorDomainPolicy>
{
    public void Configure(EntityTypeBuilder<ConnectorDomainPolicy> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.Provider, x.Domain }).IsUnique();
    }
}
