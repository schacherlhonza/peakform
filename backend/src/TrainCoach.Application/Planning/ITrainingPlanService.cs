using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Planning;

public interface ITrainingPlanService
{
    Task<IReadOnlyList<TrainingPlanDto>> GetPlansForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<TrainingPlanDetailDto> GetPlanDetailAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<TrainingPlanDto> CreatePlanAsync(Guid coachUserId, CreateTrainingPlanRequest request, CancellationToken cancellationToken = default);

    Task<TrainingWeekDto> CreateWeekAsync(Guid planId, CreateTrainingWeekRequest request, CancellationToken cancellationToken = default);
    Task<TrainingWeekDto> UpdateWeekAsync(Guid weekId, AppRole callerRole, UpdateTrainingWeekRequest request, CancellationToken cancellationToken = default);

    Task<PlannedWorkoutDto> GetWorkoutAsync(Guid workoutId, CancellationToken cancellationToken = default);
    Task<PlannedWorkoutDto> CreateWorkoutAsync(CreatePlannedWorkoutRequest request, CancellationToken cancellationToken = default);
    Task<PlannedWorkoutDto> UpdateWorkoutAsync(Guid workoutId, UpdatePlannedWorkoutRequest request, CancellationToken cancellationToken = default);
    Task DeleteWorkoutAsync(Guid workoutId, CancellationToken cancellationToken = default);
    Task<PlannedWorkoutDto> CopyWorkoutAsync(Guid workoutId, CopyWorkoutRequest request, CancellationToken cancellationToken = default);
}
