namespace TrainCoach.Application.Nutrition;

public interface IFoodEntryService
{
    Task<IReadOnlyList<FoodEntryDto>> GetForAthleteAsync(Guid athleteUserId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<FoodEntryDto> CreateAsync(Guid callerUserId, CreateFoodEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid callerUserId, Guid entryId, CancellationToken cancellationToken = default);
}
