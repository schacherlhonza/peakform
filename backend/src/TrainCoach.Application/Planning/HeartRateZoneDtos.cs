namespace TrainCoach.Application.Planning;

public record HeartRateZoneDto(
    Guid Id,
    Guid AthleteUserId,
    int ZoneNumber,
    string Name,
    int MinBpm,
    int MaxBpm,
    int? MinPaceSecondsPerKm,
    int? MaxPaceSecondsPerKm,
    DateOnly EffectiveFromDate);

public record HeartRateZoneInput(
    int ZoneNumber,
    string Name,
    int MinBpm,
    int MaxBpm,
    int? MinPaceSecondsPerKm,
    int? MaxPaceSecondsPerKm);

/// <summary>Threshold values that go with the zones; null = not set.</summary>
public record AthleteThresholdsDto(int? ThresholdPaceSecondsPerKm);

/// <summary>Replace-all semantics: every existing zone for this athlete with the same
/// <see cref="EffectiveFromDate"/> is removed and replaced by <see cref="Zones"/>.</summary>
public record SetHeartRateZonesRequest(
    Guid AthleteUserId,
    DateOnly EffectiveFromDate,
    IReadOnlyList<HeartRateZoneInput> Zones);
