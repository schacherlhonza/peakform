namespace TrainCoach.Application.Planning;

public interface IRaceService
{
    Task<IReadOnlyList<RaceDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<RaceDto> CreateAsync(Guid callerUserId, CreateRaceRequest request, CancellationToken cancellationToken = default);
    Task<RaceDto> UpdateAsync(Guid id, UpdateRaceRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
