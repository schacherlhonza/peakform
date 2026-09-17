namespace TrainCoach.Application.Execution;

public interface IActivityService
{
    Task<IReadOnlyList<CompletedActivityDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<CompletedActivityDto> GetByIdAsync(Guid callerUserId, Guid activityId, CancellationToken cancellationToken = default);
    Task<CompletedActivityDto> CreateManualAsync(Guid callerUserId, CreateManualActivityRequest request, CancellationToken cancellationToken = default);

    /// <returns>Null when the activity has no detail streams available (not from a provider that supports them, or that provider's connection is no longer active).</returns>
    Task<ActivityStreamsDto?> GetActivityStreamsAsync(Guid callerUserId, Guid activityId, CancellationToken cancellationToken = default);

    Task<TrainingFeedbackDto> UpsertFeedbackAsync(Guid callerUserId, UpsertTrainingFeedbackRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingFeedbackDto>> GetFeedbackForAthleteAsync(Guid athleteUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
