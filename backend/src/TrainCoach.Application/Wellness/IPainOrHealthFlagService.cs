namespace TrainCoach.Application.Wellness;

public interface IPainOrHealthFlagService
{
    Task<IReadOnlyList<PainOrHealthFlagDto>> GetForAthleteAsync(Guid athleteUserId, bool activeOnly, CancellationToken cancellationToken = default);
    Task<PainOrHealthFlagDto> CreateAsync(Guid callerUserId, CreatePainOrHealthFlagRequest request, CancellationToken cancellationToken = default);
    Task<PainOrHealthFlagDto> UpdateStatusAsync(Guid callerUserId, Guid flagId, UpdateHealthFlagStatusRequest request, CancellationToken cancellationToken = default);
}
