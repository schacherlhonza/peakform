namespace TrainCoach.Application.Nutrition;

public interface IHydrationEntryService
{
    Task<IReadOnlyList<HydrationEntryDto>> GetForAthleteAsync(Guid athleteUserId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<HydrationEntryDto> CreateAsync(Guid callerUserId, CreateHydrationEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid callerUserId, Guid entryId, CancellationToken cancellationToken = default);
}
