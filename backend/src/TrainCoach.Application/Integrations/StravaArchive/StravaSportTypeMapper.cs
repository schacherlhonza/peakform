using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations.StravaArchive;

/// <summary>
/// Maps the archive's localized "Activity Type" label (cs/en) to the same <see cref="SportType"/>
/// the live Strava adapter produces from the API's <c>sport_type</c>
/// (StravaIntegrationProvider.MapSport) — the two must agree, or the matcher's hard sport check
/// would stop an archived activity from ever matching its API-synced twin on another provider.
/// </summary>
public static class StravaSportTypeMapper
{
    private static readonly Dictionary<string, SportType> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        // Running
        ["Běh"] = SportType.Running, ["Run"] = SportType.Running,
        ["Trailový běh"] = SportType.Running, ["Trail Run"] = SportType.Running,
        ["Virtuální běh"] = SportType.Running, ["Virtual Run"] = SportType.Running,

        // Cycling
        ["Jízda"] = SportType.Cycling, ["Ride"] = SportType.Cycling,
        ["Virtuální jízda"] = SportType.Cycling, ["Virtual Ride"] = SportType.Cycling,
        ["Jízda na horském kole"] = SportType.Cycling, ["Mountain Bike Ride"] = SportType.Cycling,
        ["Jízda na gravel kole"] = SportType.Cycling, ["Gravel Ride"] = SportType.Cycling,
        ["Jízda na elektrokole"] = SportType.Cycling, ["E-Bike Ride"] = SportType.Cycling,

        // Swimming
        ["Plavání"] = SportType.Swimming, ["Swim"] = SportType.Swimming,

        // Strength — Strava's "Workout" also maps here, same as the API adapter.
        ["Posilování"] = SportType.Strength, ["Weight Training"] = SportType.Strength,
        ["Trénink"] = SportType.Strength, ["Workout"] = SportType.Strength,
        ["Crossfit"] = SportType.Strength,

        // Known labels that the API adapter maps to Other — listed so they don't fall through to
        // the file-probe fallback and come back as something else.
        ["Turistika"] = SportType.Other, ["Hike"] = SportType.Other,
        ["Chůze"] = SportType.Other, ["Walk"] = SportType.Other,
        ["Běžecké lyžování"] = SportType.Other, ["Nordic Ski"] = SportType.Other,
        ["Veslování"] = SportType.Other, ["Rowing"] = SportType.Other,
        ["Jóga"] = SportType.Other, ["Yoga"] = SportType.Other,
        ["Sjezdové lyžování"] = SportType.Other, ["Alpine Ski"] = SportType.Other,
    };

    /// <summary>Null when the label isn't known — the caller then falls back to the sport
    /// recorded in the activity file itself.</summary>
    public static SportType? FromLabel(string? label) =>
        label is not null && Labels.TryGetValue(label.Trim(), out var sport) ? sport : null;

    /// <summary>Sport names as written inside FIT (<c>session.sport</c>), GPX (<c>trk/type</c>) and
    /// TCX (<c>Activity/@Sport</c>) files.</summary>
    public static SportType FromFileSport(string? fileSport) => fileSport?.Trim().ToLowerInvariant() switch
    {
        "running" or "run" or "trail_running" => SportType.Running,
        "cycling" or "biking" or "ride" or "e_biking" => SportType.Cycling,
        "swimming" or "swim" => SportType.Swimming,
        "training" or "strength_training" => SportType.Strength,
        _ => SportType.Other,
    };
}
