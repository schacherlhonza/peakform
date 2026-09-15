using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainCoach.Domain.Nutrition;

namespace TrainCoach.Infrastructure.Persistence.Configurations;

public class FoodEntryConfiguration : IEntityTypeConfiguration<FoodEntry>
{
    public void Configure(EntityTypeBuilder<FoodEntry> builder)
    {
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.AthleteUserId, x.ConsumedAtUtc });
    }
}

public class HydrationEntryConfiguration : IEntityTypeConfiguration<HydrationEntry>
{
    public void Configure(EntityTypeBuilder<HydrationEntry> builder)
    {
        builder.HasIndex(x => new { x.AthleteUserId, x.ConsumedAtUtc });
    }
}
