using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Planning;

public record WorkoutTemplateDto(
    Guid Id,
    Guid CoachUserId,
    string Name,
    SportType Sport,
    string Description,
    IReadOnlyList<WorkoutSegmentDto> Segments);

public record CreateWorkoutTemplateRequest(
    string Name,
    SportType Sport,
    string Description,
    IReadOnlyList<WorkoutSegmentDto>? Segments);

public record UpdateWorkoutTemplateRequest(
    string Name,
    SportType Sport,
    string Description,
    IReadOnlyList<WorkoutSegmentDto>? Segments);
