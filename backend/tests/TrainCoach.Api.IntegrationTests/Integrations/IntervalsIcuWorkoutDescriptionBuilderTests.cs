using FluentAssertions;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;
using TrainCoach.Integrations.IntervalsIcu;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Integrations;

/// <summary>Planned workout → intervals.icu workout text (pure, no network).</summary>
public class IntervalsIcuWorkoutDescriptionBuilderTests
{
    private static WorkoutSegment Step(int order, WorkoutSegmentType type, int? seconds = null, decimal? meters = null, int? zone = null, string? notes = null, int? repeat = null) => new()
    {
        Order = order, Type = type, DurationSeconds = seconds, DistanceMeters = meters, RepeatCount = repeat, Notes = notes,
        IntensityTargetType = zone is null ? IntensityTargetType.Free : IntensityTargetType.HeartRateZone, TargetHeartRateZoneNumber = zone,
    };

    private static PlannedWorkout Workout(SportType sport, string description, params WorkoutSegment[] segments)
    {
        var workout = new PlannedWorkout { Sport = sport, Title = "Intervaly", CoachDescription = description, PlannedDurationSeconds = 3600 };
        foreach (var s in segments) workout.Segments.Add(s);
        return workout;
    }

    private static WorkoutSegment[] Block(int order, int repeat, params WorkoutSegment[] steps)
    {
        var block = new WorkoutSegment { Order = order, Type = WorkoutSegmentType.Repeat, RepeatCount = repeat };
        foreach (var s in steps) s.ParentSegment = block;
        return [block, .. steps];
    }

    [Fact]
    public void Repeat_block_becomes_an_Nx_section_with_blank_lines_around_it()
    {
        var workout = Workout(SportType.Running, "",
        [
            Step(1, WorkoutSegmentType.WarmUp, seconds: 900, zone: 2),
            .. Block(2, 6, Step(1, WorkoutSegmentType.Interval, meters: 1000, zone: 4), Step(2, WorkoutSegmentType.Recovery, seconds: 120, notes: "Klusem")),
            Step(3, WorkoutSegmentType.CoolDown, seconds: 600),
        ]);

        var result = IntervalsIcuWorkoutDescriptionBuilder.Build(workout)!;

        result.Description.Should().Be(
            "- 15m Z2 HR intensity=warmup\n" +
            "\n" +
            "6x\n" +
            "- 1km Z4 HR intensity=interval\n" +
            "- Klusem 2m intensity=recovery\n" +
            "\n" +
            "- 10m intensity=cooldown");
        result.Type.Should().Be("Run");
        result.Target.Should().Be("HR");
        result.MovingTimeSeconds.Should().BeNull("intervals.icu computes it from the steps");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Single_repeated_step_is_a_one_step_block_and_plain_steps_stay_adjacent()
    {
        var workout = Workout(SportType.Running, "",
            Step(1, WorkoutSegmentType.WarmUp, seconds: 600),
            Step(2, WorkoutSegmentType.Main, seconds: 1200),
            Step(3, WorkoutSegmentType.Strides, meters: 100, repeat: 5));

        IntervalsIcuWorkoutDescriptionBuilder.Build(workout)!.Description.Should().Be(
            "- 10m intensity=warmup\n- 20m intensity=active\n\n5x\n- Stupňované úseky 100mtr intensity=interval");
    }

    [Fact]
    public void Formats_every_target_and_length_kind()
    {
        var workout = Workout(SportType.Cycling, "",
            new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.Main, DurationSeconds = 3930, IntensityTargetType = IntensityTargetType.Power, TargetPowerWatts = 220 },
            new WorkoutSegment { Order = 2, Type = WorkoutSegmentType.Interval, DistanceMeters = 1500, IntensityTargetType = IntensityTargetType.Pace, TargetPaceSecondsPerKmMin = 290, TargetPaceSecondsPerKmMax = 270 },
            new WorkoutSegment { Order = 3, Type = WorkoutSegmentType.Main, IntensityTargetType = IntensityTargetType.Rpe, TargetRpe = 6 },
            new WorkoutSegment { Order = 4, Type = WorkoutSegmentType.Main, DurationSeconds = 45, IntensityTargetType = IntensityTargetType.Pace, TargetPaceSecondsPerKmMin = 240 });

        var result = IntervalsIcuWorkoutDescriptionBuilder.Build(workout)!;

        result.Description.Split('\n').Should().Equal(
            "- 1h5m30s 220w intensity=active",
            "- 1500mtr 4:30-4:50/km Pace intensity=interval",
            "- RPE 6 Press lap 1m intensity=active",
            "- 45s 4:00/km Pace intensity=active");
        result.Type.Should().Be("Ride");
        result.Target.Should().Be("PACE");
        result.Warnings.Should().Contain(IntervalsIcuExportWarning.RpeSentAsText);
    }

    [Fact]
    public void Free_text_cannot_be_read_as_workout_syntax()
    {
        var workout = Workout(SportType.Running,
            "WU 15min, 6x1000m v Z4 tempu.\n- pak volně 80% max",
            Step(1, WorkoutSegmentType.Interval, seconds: 300, zone: 4, notes: "Z4 tempo, 2m klus mezi"));

        var description = IntervalsIcuWorkoutDescriptionBuilder.Build(workout)!.Description;

        description.Should().Be(
            "WU 15min, 6×1000 m v zóna 4 tempu.\n" +
            "• pak volně 80 % max\n" +
            "\n" +
            "- zóna 4 tempo, 2 m klus mezi 5m Z4 HR intensity=interval");
    }

    [Fact]
    public void Workout_without_structure_sends_the_coach_text_and_planned_time()
    {
        var result = IntervalsIcuWorkoutDescriptionBuilder.Build(Workout(SportType.Running, "45 min volně"))!;

        result.Description.Should().Be("45 min volně");
        result.MovingTimeSeconds.Should().Be(3600);
        result.Target.Should().Be("AUTO");
    }

    [Fact]
    public void Rest_day_is_not_pushed()
    {
        IntervalsIcuWorkoutDescriptionBuilder.Build(new PlannedWorkout { IsRestDay = true, Sport = SportType.Rest }).Should().BeNull();
    }

    [Fact]
    public void Warns_about_what_will_not_reach_the_watch()
    {
        var many = Enumerable.Range(1, 51).Select(i => Step(i, WorkoutSegmentType.Main, seconds: 60)).ToArray();

        IntervalsIcuWorkoutDescriptionBuilder.Build(Workout(SportType.Strength, "", many))!.Warnings
            .Should().BeEquivalentTo([IntervalsIcuExportWarning.TooManySteps, IntervalsIcuExportWarning.OnlyTimedStepsSurvive]);
        IntervalsIcuWorkoutDescriptionBuilder.Build(Workout(SportType.Swimming, "", Step(1, WorkoutSegmentType.Main, meters: 400)))!.Warnings
            .Should().Equal(IntervalsIcuExportWarning.SwimPoolLengthMissing);
        IntervalsIcuWorkoutDescriptionBuilder.Build(Workout(SportType.Running, "",
                new WorkoutSegment { Order = 1, Type = WorkoutSegmentType.Main, DurationSeconds = 180, IntensityTargetType = IntensityTargetType.Power, TargetPowerWatts = 250 }))!.Warnings
            .Should().Equal(IntervalsIcuExportWarning.RunPowerMayNotShow);
    }

    [Theory]
    [InlineData(SportType.Running, "Run")]
    [InlineData(SportType.Cycling, "Ride")]
    [InlineData(SportType.Swimming, "Swim")]
    [InlineData(SportType.Strength, "WeightTraining")]
    [InlineData(SportType.CrossTraining, "Workout")]
    public void Maps_sport_to_intervals_icu_type(SportType sport, string expected)
    {
        IntervalsIcuWorkoutDescriptionBuilder.MapType(sport).Should().Be(expected);
    }
}
