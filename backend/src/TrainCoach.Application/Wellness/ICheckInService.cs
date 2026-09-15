using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

public interface ICheckInService
{
    Task<DailyCheckInDto> SubmitAsync(Guid callerUserId, SubmitCheckInRequest request, CancellationToken cancellationToken = default);
    Task<DailyCheckInDto?> GetAsync(Guid athleteUserId, DateOnly date, CheckInType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyCheckInDto>> GetRangeAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
