namespace TrainCoach.Domain.Enums;

public enum SportType
{
    Running = 1,
    Cycling = 2,
    Swimming = 3,
    Strength = 4,
    CrossTraining = 5,
    Rest = 6,
    Other = 7,
}

public enum GoalPriority
{
    A = 1,
    B = 2,
    C = 3,
}

public enum WorkoutSegmentType
{
    WarmUp = 1,
    Main = 2,
    Interval = 3,
    Rest = 4,
    CoolDown = 5,
    Repeat = 6,
    Drill = 7,
    Strides = 8,
}

public enum IntensityTargetType
{
    Free = 1,
    HeartRateZone = 2,
    Pace = 3,
    Rpe = 4,
    Power = 5,
}
