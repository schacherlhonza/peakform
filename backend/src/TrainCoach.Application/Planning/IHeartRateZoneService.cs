namespace TrainCoach.Application.Planning;

public interface IHeartRateZoneService
{
    Task<IReadOnlyList<HeartRateZoneDto>> GetForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeartRateZoneDto>> SetZonesAsync(Guid callerUserId, SetHeartRateZonesRequest request, CancellationToken cancellationToken = default);
    Task<AthleteThresholdsDto> GetThresholdsAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
    Task<AthleteThresholdsDto> SetThresholdsAsync(Guid athleteUserId, AthleteThresholdsDto thresholds, CancellationToken cancellationToken = default);
}
