using FluentAssertions;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Application.Tests.Wellness;

public class TrainingLoadCalculatorTests
{
    private static readonly HeartRateParameters Athlete = new(RestingBpm: 50, MaxBpm: 190, ThresholdBpm: 162, Female: false);

    private static (int[] Time, int?[] Hr) Steady(int seconds, int bpm) =>
        (Enumerable.Range(0, seconds + 1).ToArray(), Enumerable.Repeat<int?>(bpm, seconds + 1).ToArray());

    [Fact]
    public void One_hour_at_threshold_is_100_points()
    {
        var (time, hr) = Steady(3600, 162);

        TrainingLoadCalculator.FromStream(time, hr, Athlete).Should().Be(100m);
    }

    [Fact]
    public void Easy_hour_scores_well_below_threshold_hour_and_summary_estimate_matches_steady_stream()
    {
        var (time, hr) = Steady(3600, 130);

        var stream = TrainingLoadCalculator.FromStream(time, hr, Athlete)!.Value;

        stream.Should().BeInRange(35m, 55m);
        TrainingLoadCalculator.FromSummary(130, 3600, Athlete).Should().Be(stream);
    }

    [Fact]
    public void Pauses_add_no_load()
    {
        int[] time = [0, 1800, 1801];
        int?[] hr = [160, 160, 160];

        TrainingLoadCalculator.FromStream(time, hr, Athlete)!.Value.Should().BeLessThan(1m, "only the last 1 s counts");
    }

    [Fact]
    public void Ctl_and_atl_follow_daily_load_and_decay_on_rest_days()
    {
        var start = new DateOnly(2026, 1, 1);
        var loads = Enumerable.Range(0, 28).ToDictionary(i => start.AddDays(i), _ => 100m);

        var daily = TrainingLoadCalculator.Daily(loads, start, start.AddDays(41));

        var lastTraining = daily[27];
        lastTraining.Atl.Should().BeGreaterThan(lastTraining.Ctl, "fatigue builds faster than fitness");
        lastTraining.RampRate.Should().BeGreaterThan(0);
        daily[^1].Atl.Should().BeLessThan(daily[^1].Ctl, "after two rest weeks fatigue has dropped below fitness");
        daily[^1].Ctl.Should().BeLessThan(lastTraining.Ctl);
        daily[0].RampRate.Should().BeNull("no 7-day history yet");
    }
}
