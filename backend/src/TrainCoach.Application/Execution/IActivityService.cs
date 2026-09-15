namespace TrainCoach.Application.Execution;

public interface IActivityService
{
    Task<IReadOnlyList<CompletedActivityDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<CompletedActivityDto> CreateManualAsync(Guid callerUserId, CreateManualActivityRequest request, CancellationToken cancellationToken = default);

    Task<TrainingFeedbackDto> UpsertFeedbackAsync(Guid callerUserId, UpsertTrainingFeedbackRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingFeedbackDto>> GetFeedbackForAthleteAsync(Guid athleteUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
