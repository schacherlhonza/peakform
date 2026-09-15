namespace TrainCoach.Application.Wellness;

public interface IPersonalRecordService
{
    Task<IReadOnlyList<PersonalRecordDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<PersonalRecordDto> CreateAsync(Guid callerUserId, CreatePersonalRecordRequest request, CancellationToken cancellationToken = default);
    Task<PersonalRecordDto> UpdateAsync(Guid callerUserId, Guid recordId, UpdatePersonalRecordRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid callerUserId, Guid recordId, CancellationToken cancellationToken = default);
}
