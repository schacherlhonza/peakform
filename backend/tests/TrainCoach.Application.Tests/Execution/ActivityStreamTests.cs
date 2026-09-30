using FluentAssertions;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Tests.Execution;

public class ActivityStreamTests
{
    private static readonly DateTime Start = new(2024, 5, 1, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>A 1 Hz run heading north at 3 m/s, with a 5-minute pause in the middle.</summary>
    private static ActivityStreamData Run(int seconds, int pauseAt = -1, int pauseLength = 0)
    {
        var builder = new ActivityStreamBuilder();
        for (var s = 0; s < seconds; s++)
        {
            var t = s >= pauseAt && pauseAt >= 0 ? s + pauseLength : s;
            builder.Add(new ActivityStreamBuilder.Sample(
                Start.AddSeconds(t), HeartRate: 140 + s % 20, Cadence: 85, Distance: s * 3.0, Altitude: 200 + s * 0.15,
                Latitude: 50.0 + s * 3.0 / 111_195, Longitude: 14.4, Temperature: 21));
        }
        return builder.Build()!;
    }

    [Fact]
    public void Codec_roundtrips_all_channels_including_gaps()
    {
        var builder = new ActivityStreamBuilder();
        builder.Add(new ActivityStreamBuilder.Sample(Start, HeartRate: 120, Power: 250, Latitude: 50.087451, Longitude: 14.420671, Altitude: 191.4));
        builder.Add(new ActivityStreamBuilder.Sample(Start.AddSeconds(1), HeartRate: null, Power: 260, Latitude: 50.087460, Longitude: 14.420690, Altitude: 191.6));
        builder.Add(new ActivityStreamBuilder.Sample(Start.AddSeconds(5), HeartRate: 124, Power: null, Latitude: null, Longitude: null, Altitude: 192.0));
        var data = builder.Build()!;

        var decoded = ActivityStreamCodec.Decode(ActivityStreamCodec.Encode(data), ActivityStreamCodec.CurrentFormatVersion, data.OriginalSampleCount);

        decoded.TimeOffsetsSeconds.Should().Equal(0, 1, 5);
        decoded.HeartRateBpm.Should().Equal(120, null, 124);
        decoded.PowerWatts.Should().Equal(250, 260, null);
        decoded.Latitude![0].Should().BeApproximately(50.087451, 1e-6);
        decoded.Latitude[2].Should().BeNull();
        decoded.AltitudeMeters![1].Should().BeApproximately(191.6, 0.05);
        decoded.CadenceRpm.Should().BeNull("a channel never recorded stays absent");
    }

    [Fact]
    public void Codec_is_compact_for_a_typical_hour()
    {
        var data = ActivityStreamDownsampler.Downsample(Run(3600));

        var payload = ActivityStreamCodec.Encode(data);

        payload.Length.Should().BeLessThan(20 * 1024);
    }

    [Fact]
    public void Short_stream_is_kept_whole()
    {
        var data = Run(1500);

        ActivityStreamDownsampler.Downsample(data).Should().BeSameAs(data);
    }

    [Fact]
    public void Long_stream_is_capped_aligned_and_keeps_first_and_last()
    {
        var data = Run(5 * 3600);

        var small = ActivityStreamDownsampler.Downsample(data);

        small.Count.Should().BeLessThanOrEqualTo(ActivityStreamDownsampler.DefaultMaxPoints);
        small.IsDownsampled.Should().BeTrue();
        small.OriginalSampleCount.Should().Be(5 * 3600);
        small.TimeOffsetsSeconds[0].Should().Be(0);
        small.TimeOffsetsSeconds[^1].Should().Be(data.TimeOffsetsSeconds[^1]);
        small.TimeOffsetsSeconds.Should().BeInAscendingOrder();
        foreach (var channel in new object?[] { small.HeartRateBpm, small.CadenceRpm, small.DistanceMeters, small.AltitudeMeters, small.Latitude, small.Longitude, small.TemperatureC })
        {
            ((System.Collections.ICollection)channel!).Count.Should().Be(small.Count);
        }
        small.HeartRateBpm!.Should().OnlyContain(hr => hr >= 140 && hr <= 159, "averaged within the recorded range");
        small.DistanceMeters![^1].Should().Be(data.DistanceMeters![^1]);
    }

    [Fact]
    public void Pause_stays_a_gap_instead_of_being_filled()
    {
        var data = Run(2 * 3600, pauseAt: 3600, pauseLength: 1800);

        var small = ActivityStreamDownsampler.Downsample(data);

        small.TimeOffsetsSeconds.Should().NotContain(t => t > 3600 && t < 3600 + 1800);
    }

    [Fact]
    public void Builder_sorts_dedupes_and_derives_distance_and_speed_from_gps()
    {
        var builder = new ActivityStreamBuilder();
        builder.Add(new ActivityStreamBuilder.Sample(Start.AddSeconds(10), Latitude: 50.0 + 30.0 / 111_195, Longitude: 14.4));
        builder.Add(new ActivityStreamBuilder.Sample(Start, Latitude: 50.0, Longitude: 14.4));
        builder.Add(new ActivityStreamBuilder.Sample(Start, Latitude: 50.0, Longitude: 14.4)); // duplicate timestamp
        builder.Add(new ActivityStreamBuilder.Sample(Start.AddSeconds(5), Latitude: 0, Longitude: 0)); // "no fix"

        var data = builder.Build()!;

        data.TimeOffsetsSeconds.Should().Equal(0, 5, 10);
        data.Latitude![1].Should().BeNull();
        data.DistanceMeters![2].Should().BeApproximately(30, 0.5);
        data.SpeedMetersPerSecond![2].Should().BeApproximately(3, 0.05);
        data.Channels.Should().HaveFlag(ActivityStreamChannels.Position).And.HaveFlag(ActivityStreamChannels.Speed);
    }

    [Fact]
    public void Grade_is_measured_over_the_trailing_window()
    {
        var data = Run(600); // 0.15 m up per 3 m = 5 %

        var grade = ActivityStreamMapping.Grade(data.DistanceMeters, data.AltitudeMeters)!;

        grade.Take(10).Should().OnlyContain(g => g == null, "less than 30 m covered yet");
        grade[100].Should().BeApproximately(5m, 0.1m);
    }

    [Fact]
    public void Running_cadence_is_reported_in_steps_per_minute()
    {
        ActivityStreamMapping.StepsPerMinute(SportType.Running, [85, null]).Should().Equal(170, null);
        ActivityStreamMapping.StepsPerMinute(SportType.Cycling, [90]).Should().Equal(90);
    }
}
