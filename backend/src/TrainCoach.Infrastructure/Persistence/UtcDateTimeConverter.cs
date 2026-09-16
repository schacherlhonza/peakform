using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TrainCoach.Infrastructure.Persistence;

/// <summary>
/// Every DateTime property in the domain represents a UTC instant (the "...Utc" naming
/// convention is enforced throughout). Npgsql maps DateTime to "timestamp with time zone" and
/// throws if a value's Kind isn't already Utc — values built via DateOnly.ToDateTime(...) or
/// other Kind-less constructions have Kind=Unspecified. This converter stamps Kind=Utc on
/// write/read so every call site is safe without having to remember DateTime.SpecifyKind.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
