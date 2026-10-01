using FluentAssertions;
using TrainCoach.Application.Execution;
using TrainCoach.Application.Execution.Streams;

namespace TrainCoach.Application.Tests.Execution;

public class HrZoneTimeCalculatorTests
{
    private static readonly HrZoneBounds[] Zones =
    [
        new(1, 0, 144), new(2, 145, 155), new(3, 156, 161), new(4, 162, 167), new(5, 168, 190),
    ];

    [Fact]
    public void Each_sample_holds_until_the_next_one()
    {
        int[] time = [0, 10, 20, 30, 40];
        int?[] hr = [140, 150, 158, 165, 170];

        var result = HrZoneTimeCalculator.Compute(time, hr, Zones);

        result.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 10, [2] = 10, [3] = 10, [4] = 10 },
            "the last sample has no duration");
    }

    [Fact]
    public void Pauses_and_missing_heart_rate_count_nothing()
    {
        int[] time = [0, 5, 600, 605, 610];
        int?[] hr = [150, 150, 150, null, 150];

        var result = HrZoneTimeCalculator.Compute(time, hr, Zones);

        result.Should().BeEquivalentTo(new Dictionary<int, int> { [2] = 10 }, "0→5 and 600→605 count; the 595 s gap is a pause");
    }

    [Fact]
    public void Above_the_top_zone_counts_in_it_below_the_lowest_counts_apart()
    {
        int[] time = [0, 10, 20];
        int?[] hr = [200, 84, 84];

        var result = HrZoneTimeCalculator.Compute(time, hr, [new(1, 130, 144), new(2, 145, 180)]);

        result.Should().BeEquivalentTo(new Dictionary<int, int> { [2] = 10, [HrZoneTimeCalculator.BelowZones] = 10 },
            "84 bpm with zone 1 starting at 130 is in no zone");
    }

    [Fact]
    public void With_speed_only_moving_time_counts()
    {
        // A walk with the watch left recording while standing: 20 s moving, 20 s standing.
        int[] time = [0, 10, 20, 30, 40, 41];
        int?[] hr = [150, 150, 150, 150, 150, 150];
        double?[] speed = [1.2, 0.0, 1.3, 0.2, 1.1, 1.1];

        var result = HrZoneTimeCalculator.Compute(time, hr, Zones, speed);

        result.Should().BeEquivalentTo(new Dictionary<int, int> { [2] = 21 });
    }

    [Fact]
    public void Zone_set_in_effect_on_the_date_and_the_earliest_set_before_any()
    {
        var early = new List<HrZoneBounds> { new(1, 0, 150) };
        var later = new List<HrZoneBounds> { new(1, 0, 140) };
        var sets = new List<(DateOnly, List<HrZoneBounds>)> { (new DateOnly(2025, 1, 1), early), (new DateOnly(2026, 6, 1), later) };

        HrZoneRecomputeJob.ZonesOn(sets, new DateOnly(2017, 5, 1)).Should().BeSameAs(early);
        HrZoneRecomputeJob.ZonesOn(sets, new DateOnly(2026, 1, 1)).Should().BeSameAs(early);
        HrZoneRecomputeJob.ZonesOn(sets, new DateOnly(2026, 6, 1)).Should().BeSameAs(later);
    }
}
