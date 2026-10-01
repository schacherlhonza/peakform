using FluentAssertions;
using TrainCoach.Application.Common;

namespace TrainCoach.Application.Tests.Common;

public class AthleteLocalDatesTests
{
    private static readonly TimeZoneInfo Prague = AthleteLocalDates.ResolveTimeZone("Europe/Prague");

    [Fact]
    public void Local_midnight_in_summer_and_winter()
    {
        AthleteLocalDates.StartOfDayUtc(new DateOnly(2026, 9, 28), Prague).Should().Be(new DateTime(2026, 9, 27, 22, 0, 0, DateTimeKind.Utc));
        AthleteLocalDates.StartOfDayUtc(new DateOnly(2026, 1, 5), Prague).Should().Be(new DateTime(2026, 1, 4, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Range_upper_bound_is_the_next_local_midnight_across_a_dst_change()
    {
        // 2026-10-25 is the switch back to winter time in Prague: that day is 25 hours long.
        var (from, to) = AthleteLocalDates.ToUtcRange(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 25), Prague);

        from.Should().Be(new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc));
        to.Should().Be(new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Unknown_time_zone_falls_back_to_prague()
    {
        AthleteLocalDates.ResolveTimeZone("Not/AZone").BaseUtcOffset.Should().Be(Prague.BaseUtcOffset);
    }
}
