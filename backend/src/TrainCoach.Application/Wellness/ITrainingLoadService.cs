using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Wellness;

/// <summary>Read-only: CTL/ATL/ramp rate are computed (by a provider, or by PeakForm from heart
/// rate), nobody manually enters their own — see <see cref="TrainCoach.Domain.Wellness.TrainingLoadSnapshot"/>.</summary>
public interface ITrainingLoadService
{
    /// <param name="source">One source's series (e.g. PeakForm for a consistent long history);
    /// null = one row per day, the highest-precedence source of that day.</param>
    Task<IReadOnlyList<TrainingLoadSnapshotDto>> GetForAthleteAsync(Guid athleteUserId, DateOnly from, DateOnly to, DataSource? source = null, CancellationToken cancellationToken = default);
}
