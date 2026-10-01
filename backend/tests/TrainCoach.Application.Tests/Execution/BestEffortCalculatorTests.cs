using FluentAssertions;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Tests.Execution;

public class BestEffortCalculatorTests
{
    /// <summary>1 Hz run: <paramref name="slowSeconds"/> at 3 m/s, then <paramref name="fastSeconds"/> at 4 m/s.</summary>
    private static (int[] Time, double?[] Distance) Run(int slowSeconds, int fastSeconds)
    {
        var n = slowSeconds + fastSeconds + 1;
        var time = Enumerable.Range(0, n).ToArray();
        var distance = new double?[n];
        double d = 0;
        for (var s = 0; s < n; s++)
        {
            distance[s] = d;
            d += s < slowSeconds ? 3 : 4;
        }
        return (time, distance);
    }

    [Fact]
    public void Finds_the_fastest_kilometre_with_interpolated_start()
    {
        var (time, distance) = Run(slowSeconds: 600, fastSeconds: 600); // 1.8 km slow, then 2.4 km fast

        var efforts = BestEffortCalculator.DistanceEfforts(time, distance).ToList();

        efforts.Single(e => e.Type == BestEffortType.Distance1Km).Value.Should().Be(250m, "1000 m at 4 m/s");
        efforts.Should().NotContain(e => e.Type == BestEffortType.Distance5Km, "the run is only 4.2 km");
    }

    [Fact]
    public void Wandering_gps_jumps_are_dropped()
    {
        var (time, distance) = Run(slowSeconds: 1200, fastSeconds: 0); // 3.6 km at 3 m/s = 5:33 /km
        // Ten 150 m jumps one second apart: a "50 m/s" stretch.
        for (var s = 300; s < time.Length; s++)
        {
            distance[s] += 150 * Math.Min(10, s - 299);
        }

        var km = BestEffortCalculator.DistanceEfforts(time, distance).Single(e => e.Type == BestEffortType.Distance1Km);

        km.Value.Should().BeApproximately(333.3m, 1m);
    }

    [Fact]
    public void A_catch_up_step_after_signal_loss_is_kept_as_real_distance()
    {
        var (time, distance) = Run(slowSeconds: 3000, fastSeconds: 0); // 9 km at 3 m/s
        // Signal lost 40 s: distance stalls, then catches up in one step (120 m in 1 s).
        for (var s = 1000; s < 1040; s++)
        {
            distance[s] = distance[999];
        }

        var efforts = BestEffortCalculator.DistanceEfforts(time, distance).ToList();

        efforts.Should().Contain(e => e.Type == BestEffortType.Distance5Km, "the stalled distance comes back at the step");
    }

    [Fact]
    public void Best_power_windows_ignore_pauses_and_sensor_spikes()
    {
        var time = Enumerable.Range(0, 1801).ToArray();
        var power = time.Select(s => (int?)(s is >= 600 and < 900 ? 300 : 200)).ToArray();
        power[100] = 3664; // dropout spike

        var efforts = BestEffortCalculator.PowerEfforts(time, power, maxWatts: 1000).ToDictionary(e => e.Type, e => e.Value);

        efforts[BestEffortType.Power1Min].Should().Be(300m);
        efforts[BestEffortType.Power5Min].Should().Be(300m);
        efforts[BestEffortType.Power20Min].Should().Be(225m); // 300 s at 300 W + 900 s at 200 W
    }

    [Fact]
    public void Distance_efforts_only_for_runs()
    {
        var (time, distance) = Run(slowSeconds: 2000, fastSeconds: 0);
        var data = new ActivityStreamData { TimeOffsetsSeconds = time, DistanceMeters = distance, OriginalSampleCount = time.Length };

        BestEffortCalculator.Compute(SportType.Cycling, data).Should().BeEmpty();
        BestEffortCalculator.Compute(SportType.Running, data).Should().Contain(e => e.Type == BestEffortType.Distance5Km);
    }
}
