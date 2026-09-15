namespace TrainCoach.Domain.Enums;

public enum CheckInType
{
    Morning = 1,
    Evening = 2,
}

/// <summary>1 (worst) to 5 (best) subjective scale, used for sleep quality, energy, mood, etc.</summary>
public enum WellnessScale
{
    VeryPoor = 1,
    Poor = 2,
    Moderate = 3,
    Good = 4,
    VeryGood = 5,
}

public enum HealthFlagType
{
    Pain = 1,
    Illness = 2,
    Injury = 3,
    Other = 4,
}

public enum HealthFlagSeverity
{
    Mild = 1,
    Moderate = 2,
    Severe = 3,
}

public enum HealthFlagStatus
{
    Active = 1,
    Improving = 2,
    Resolved = 3,
}

/// <summary>Metric a rolling <see cref="TrainCoach.Domain.Wellness.PerformanceBaseline"/> is computed for.</summary>
public enum BaselineMetricType
{
    RestingHeartRate = 1,
    Hrv = 2,
    SleepDurationMinutes = 3,
    ReadinessScore = 4,
}
