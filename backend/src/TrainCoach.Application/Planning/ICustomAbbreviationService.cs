namespace TrainCoach.Application.Planning;

public interface ICustomAbbreviationService
{
    Task<IReadOnlyList<CustomAbbreviationDto>> GetForCoachAsync(Guid coachUserId, CancellationToken cancellationToken = default);
    Task<CustomAbbreviationDto> CreateAsync(Guid coachUserId, CreateCustomAbbreviationRequest request, CancellationToken cancellationToken = default);
    Task<CustomAbbreviationDto> UpdateAsync(Guid callerUserId, Guid id, UpdateCustomAbbreviationRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid callerUserId, Guid id, CancellationToken cancellationToken = default);
}
