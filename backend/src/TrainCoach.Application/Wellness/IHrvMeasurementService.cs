namespace TrainCoach.Application.Wellness;

public interface IHrvMeasurementService
{
    Task<IReadOnlyList<HrvMeasurementDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<HrvMeasurementDto> CreateOrUpdateAsync(Guid callerUserId, UpsertHrvMeasurementRequest request, CancellationToken cancellationToken = default);
}
