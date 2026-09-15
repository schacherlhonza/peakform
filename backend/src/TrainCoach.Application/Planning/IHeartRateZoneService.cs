namespace TrainCoach.Application.Planning;

public interface IHeartRateZoneService
{
    Task<IReadOnlyList<HeartRateZoneDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeartRateZoneDto>> SetZonesAsync(Guid callerUserId, SetHeartRateZonesRequest request, CancellationToken cancellationToken = default);
}
