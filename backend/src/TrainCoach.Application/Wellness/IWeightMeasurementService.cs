namespace TrainCoach.Application.Wellness;

public interface IWeightMeasurementService
{
    Task<IReadOnlyList<WeightMeasurementDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<WeightMeasurementDto> CreateOrUpdateAsync(Guid callerUserId, UpsertWeightMeasurementRequest request, CancellationToken cancellationToken = default);
}
