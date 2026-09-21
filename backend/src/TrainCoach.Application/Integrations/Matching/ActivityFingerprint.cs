using System.Globalization;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations.Matching;

/// <summary>
/// Computes the deterministic, deliberately coarse candidate-index fingerprint used by
/// IActivityMatchingService's level-4 lookup. This is only ever a candidate index — two
/// activities sharing a fingerprint are *candidates* for level-5 confidence scoring, never
/// themselves proof of a match (see docs/integrations/activity-matching.md).
/// </summary>
public static class ActivityFingerprint
{
    private const int TimeBucketSeconds = 5 * 60;
    private const int DurationBucketSeconds = 60;
    private const int DistanceBucketMeters = 100;

    public static string Compute(SportType sport, DateTime startedAtUtc, int durationSeconds, decimal? distanceMeters, string? deviceName)
    {
        var startBucket = RoundToBucket(new DateTimeOffset(DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(), TimeBucketSeconds);
        var durationBucket = RoundToBucket(durationSeconds, DurationBucketSeconds);
        var distancePart = distanceMeters is { } d
            ? RoundToBucket((long)d, DistanceBucketMeters).ToString(CultureInfo.InvariantCulture)
            : "NODIST";
        var devicePart = string.IsNullOrWhiteSpace(deviceName) ? "NODEVICE" : deviceName.Trim().ToLowerInvariant();

        return $"{sport}|{startBucket}|{durationBucket}|{distancePart}|{devicePart}";
    }

    private static long RoundToBucket(long value, int bucketSize) => (long)Math.Round(value / (double)bucketSize) * bucketSize;
}
