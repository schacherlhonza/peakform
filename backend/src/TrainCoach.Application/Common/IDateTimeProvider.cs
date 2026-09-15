namespace TrainCoach.Application.Common;

/// <summary>All storage/comparison happens in UTC; the presentation layer converts to the user's timezone.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}
