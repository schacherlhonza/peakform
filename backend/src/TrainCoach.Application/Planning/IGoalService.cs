namespace TrainCoach.Application.Planning;

public interface IGoalService
{
    Task<IReadOnlyList<GoalDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<GoalDto> CreateAsync(Guid callerUserId, CreateGoalRequest request, CancellationToken cancellationToken = default);
    Task<GoalDto> UpdateAsync(Guid id, UpdateGoalRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
