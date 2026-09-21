namespace TrainCoach.Application.Wellness;

/// <summary>Read-only: CTL/ATL/ramp rate are provider-computed, nobody manually enters their own — see <see cref="TrainCoach.Domain.Wellness.TrainingLoadSnapshot"/>.</summary>
public interface ITrainingLoadService
{
    Task<IReadOnlyList<TrainingLoadSnapshotDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
