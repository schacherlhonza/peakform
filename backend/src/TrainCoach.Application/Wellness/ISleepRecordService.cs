namespace TrainCoach.Application.Wellness;

public interface ISleepRecordService
{
    Task<IReadOnlyList<SleepRecordDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<SleepRecordDto> CreateOrUpdateAsync(Guid callerUserId, UpsertSleepRecordRequest request, CancellationToken cancellationToken = default);
}
