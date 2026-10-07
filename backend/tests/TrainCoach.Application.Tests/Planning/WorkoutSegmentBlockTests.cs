using FluentAssertions;
using TrainCoach.Application.Planning;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Tests.Planning;

public class WorkoutSegmentBlockTests
{
    private static WorkoutSegmentDto Step(int order, WorkoutSegmentType type, int? seconds = null, decimal? meters = null, int? zone = null, int? repeat = null) => new(
        null, order, type, repeat, meters, seconds,
        zone is null ? IntensityTargetType.Free : IntensityTargetType.HeartRateZone,
        null, zone, null, null, null, null, null);

    private static WorkoutSegmentDto Block(int order, int repeat, params WorkoutSegmentDto[] steps) =>
        new(null, order, WorkoutSegmentType.Repeat, repeat, null, null, IntensityTargetType.Free, null, null, null, null, null, null, null, steps);

    private static readonly IReadOnlyList<WorkoutSegmentDto> Intervals =
    [
        Step(1, WorkoutSegmentType.WarmUp, seconds: 900, zone: 2),
        Block(2, 6, Step(1, WorkoutSegmentType.Interval, seconds: 300, zone: 4), Step(2, WorkoutSegmentType.Recovery, seconds: 120)),
        Step(3, WorkoutSegmentType.CoolDown, seconds: 600),
    ];

    [Fact]
    public void Blocks_survive_the_flat_entity_round_trip()
    {
        var entities = WorkoutSegmentMapping.ToEntities(Intervals);

        entities.Should().HaveCount(5, "block steps are stored flat next to the top-level segments");
        var block = entities.Single(e => e.Type == WorkoutSegmentType.Repeat);
        entities.Where(e => e.ParentSegmentId == block.Id).Should().HaveCount(2);

        var dtos = WorkoutSegmentMapping.ToDtos(entities);
        dtos.Select(d => d.Type).Should().Equal(WorkoutSegmentType.WarmUp, WorkoutSegmentType.Repeat, WorkoutSegmentType.CoolDown);
        dtos[1].Steps!.Select(s => (s.Type, s.DurationSeconds)).Should().Equal((WorkoutSegmentType.Interval, 300), (WorkoutSegmentType.Recovery, 120));
        dtos[0].Steps.Should().BeNull();
    }

    [Fact]
    public void Planned_zones_multiply_block_steps_by_block_repeats()
    {
        var workout = new PlannedWorkout { Segments = WorkoutSegmentMapping.ToEntities(Intervals) };

        var (zones, unspecified) = PlanVsActualService.PlannedZones(workout, new Dictionary<Guid, int>());

        zones[1].Should().Be(900);
        zones[3].Should().Be(6 * 300);
        unspecified.Should().Be(6 * 120 + 600);
    }

    [Fact]
    public void Valid_block_passes()
    {
        var validator = new WorkoutSegmentDtoValidator();
        Intervals.Select(s => validator.Validate(s).IsValid).Should().AllBeEquivalentTo(true);
    }

    public static TheoryData<string, WorkoutSegmentDto> InvalidSegments => new()
    {
        { "nested block", Block(1, 3, Block(1, 2, Step(1, WorkoutSegmentType.Interval, seconds: 60))) },
        { "repeated step inside a block", Block(1, 3, Step(1, WorkoutSegmentType.Interval, seconds: 60, repeat: 2)) },
        { "empty block", Block(1, 3) },
        { "block repeated once", Block(1, 1, Step(1, WorkoutSegmentType.Interval, seconds: 60)) },
        { "block with its own length", Block(1, 3, Step(1, WorkoutSegmentType.Interval, seconds: 60)) with { DurationSeconds = 600 } },
        { "steps on a plain segment", Step(1, WorkoutSegmentType.Main, seconds: 600) with { Steps = [Step(1, WorkoutSegmentType.Interval, seconds: 60)] } },
        { "both time and distance", Step(1, WorkoutSegmentType.Main, seconds: 600, meters: 2000) },
    };

    [Theory]
    [MemberData(nameof(InvalidSegments))]
    public void Invalid_structures_are_rejected(string because, WorkoutSegmentDto segment)
    {
        new WorkoutSegmentDtoValidator().Validate(segment).IsValid.Should().BeFalse(because);
    }
}
