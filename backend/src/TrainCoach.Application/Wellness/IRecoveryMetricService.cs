namespace TrainCoach.Application.Wellness;

public interface IRecoveryMetricService
{
    Task<IReadOnlyList<RecoveryMetricDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<RecoveryMetricDto> CreateOrUpdateAsync(Guid callerUserId, UpsertRecoveryMetricRequest request, CancellationToken cancellationToken = default);
}
