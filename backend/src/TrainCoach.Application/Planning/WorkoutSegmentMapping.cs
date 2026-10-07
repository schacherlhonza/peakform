using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

/// <summary>
/// Segment mapping shared by planned workouts and templates (same <see cref="WorkoutSegment"/> shape).
/// The DTO is a tree (repeat block → steps); entities are a flat list linked by <c>ParentSegmentId</c>,
/// all owned by the workout/template, so callers add/remove the whole list at once.
/// </summary>
internal static class WorkoutSegmentMapping
{
    public static List<WorkoutSegment> ToEntities(IReadOnlyList<WorkoutSegmentDto>? segments)
    {
        var result = new List<WorkoutSegment>();
        foreach (var dto in segments ?? [])
        {
            var parent = ToEntity(dto, null);
            result.Add(parent);
            foreach (var step in dto.Steps ?? [])
            {
                var child = ToEntity(step, parent);
                parent.Steps.Add(child);
                result.Add(child);
            }
        }
        return result;
    }

    public static List<WorkoutSegment> Copy(IEnumerable<WorkoutSegment> segments) =>
        ToEntities(ToDtos(segments));

    public static List<WorkoutSegmentDto> ToDtos(IEnumerable<WorkoutSegment> segments)
    {
        var byParent = segments.ToLookup(s => s.ParentSegmentId ?? s.ParentSegment?.Id);
        List<WorkoutSegmentDto> Level(Guid? parentId) =>
            byParent[parentId].OrderBy(s => s.Order).Select(s => ToDto(s, byParent.Contains(s.Id) ? Level(s.Id) : null)).ToList();
        return Level(null);
    }

    private static WorkoutSegment ToEntity(WorkoutSegmentDto s, WorkoutSegment? parent) => new()
    {
        ParentSegment = parent,
        ParentSegmentId = parent?.Id,
        Order = s.Order,
        Type = s.Type,
        RepeatCount = s.RepeatCount,
        DistanceMeters = s.DistanceMeters,
        DurationSeconds = s.DurationSeconds,
        IntensityTargetType = s.IntensityTargetType,
        TargetHeartRateZoneId = s.TargetHeartRateZoneId,
        TargetHeartRateZoneNumber = s.TargetHeartRateZoneNumber,
        TargetPaceSecondsPerKmMin = s.TargetPaceSecondsPerKmMin,
        TargetPaceSecondsPerKmMax = s.TargetPaceSecondsPerKmMax,
        TargetRpe = s.TargetRpe,
        TargetPowerWatts = s.TargetPowerWatts,
        Notes = s.Notes,
    };

    private static WorkoutSegmentDto ToDto(WorkoutSegment s, IReadOnlyList<WorkoutSegmentDto>? steps) => new(
        s.Id, s.Order, s.Type, s.RepeatCount, s.DistanceMeters, s.DurationSeconds, s.IntensityTargetType,
        s.TargetHeartRateZoneId, s.TargetHeartRateZoneNumber, s.TargetPaceSecondsPerKmMin, s.TargetPaceSecondsPerKmMax,
        s.TargetRpe, s.TargetPowerWatts, s.Notes, steps);
}
