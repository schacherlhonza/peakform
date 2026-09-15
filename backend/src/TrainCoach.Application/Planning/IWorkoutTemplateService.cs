namespace TrainCoach.Application.Planning;

public interface IWorkoutTemplateService
{
    Task<IReadOnlyList<WorkoutTemplateDto>> GetForCoachAsync(Guid coachUserId, CancellationToken cancellationToken = default);
    Task<WorkoutTemplateDto> CreateAsync(Guid coachUserId, CreateWorkoutTemplateRequest request, CancellationToken cancellationToken = default);
    Task<WorkoutTemplateDto> UpdateAsync(Guid callerUserId, Guid id, UpdateWorkoutTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid callerUserId, Guid id, CancellationToken cancellationToken = default);
}
