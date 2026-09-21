using FluentAssertions;
using TrainCoach.Application.Integrations.Matching;
using TrainCoach.Domain.Enums;
using Xunit;

namespace TrainCoach.Application.Tests.Integrations.Matching;

public class ActivityFingerprintTests
{
    private static readonly DateTime Start = new(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SmallTimingDifferences_ProduceTheSameFingerprint()
    {
        var a = ActivityFingerprint.Compute(SportType.Running, Start, 3600, 10000, "Garmin Forerunner 965");
        var b = ActivityFingerprint.Compute(SportType.Running, Start.AddSeconds(30), 3610, 10040, "Garmin Forerunner 965");

        a.Should().Be(b);
    }

    [Fact]
    public void DifferentSport_ProducesDifferentFingerprint()
    {
        var running = ActivityFingerprint.Compute(SportType.Running, Start, 3600, 10000, null);
        var cycling = ActivityFingerprint.Compute(SportType.Cycling, Start, 3600, 10000, null);

        running.Should().NotBe(cycling);
    }

    [Fact]
    public void MissingDistance_UsesNodistPlaceholder_NotNull()
    {
        var fingerprint = ActivityFingerprint.Compute(SportType.Strength, Start, 2700, null, null);

        fingerprint.Should().Contain("NODIST");
    }

    [Fact]
    public void DeviceNameIsNormalizedCaseInsensitively()
    {
        var a = ActivityFingerprint.Compute(SportType.Running, Start, 3600, 10000, "Garmin Forerunner 965");
        var b = ActivityFingerprint.Compute(SportType.Running, Start, 3600, 10000, "  GARMIN FORERUNNER 965  ");

        a.Should().Be(b);
    }

    [Fact]
    public void LargeTimingDifference_ProducesDifferentFingerprint()
    {
        var a = ActivityFingerprint.Compute(SportType.Running, Start, 3600, 10000, null);
        var b = ActivityFingerprint.Compute(SportType.Running, Start.AddHours(3), 3600, 10000, null);

        a.Should().NotBe(b);
    }
}
