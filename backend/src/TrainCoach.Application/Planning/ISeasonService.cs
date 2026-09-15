namespace TrainCoach.Application.Planning;

public interface ISeasonService
{
    Task<IReadOnlyList<SeasonDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<SeasonDto> CreateAsync(Guid callerUserId, CreateSeasonRequest request, CancellationToken cancellationToken = default);
    Task<SeasonDto> UpdateAsync(Guid id, UpdateSeasonRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
