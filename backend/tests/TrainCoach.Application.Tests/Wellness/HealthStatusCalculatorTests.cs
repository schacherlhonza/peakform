using FluentAssertions;
using TrainCoach.Application.Wellness;
using Xunit;

namespace TrainCoach.Application.Tests.Wellness;

public class HealthStatusCalculatorTests
{
    // Shaped like a real intervals.icu (Garmin) HRV history: mean ≈ 47 ms, day-to-day swings of ±7 ms.
    private static readonly decimal[] HrvHistory = [44, 50, 41, 39, 54, 53, 47, 51, 45, 45, 43];

    [Fact]
    public void ValueInsidePersonalRange_IsInRange()
    {
        var result = HealthStatusCalculator.Evaluate(HealthMetric.Hrv, 47, HrvHistory);

        result.Status.Should().Be(HealthMetricStatus.InRange);
        result.RangeLow.Should().BeLessThan(47);
        result.RangeHigh.Should().BeGreaterThan(47);
        result.IsConcerning.Should().BeFalse();
    }

    [Fact]
    public void HrvAboveRange_IsFlaggedButNotConcerning()
    {
        var result = HealthStatusCalculator.Evaluate(HealthMetric.Hrv, 62, HrvHistory);

        result.Status.Should().Be(HealthMetricStatus.AboveRange);
        result.IsConcerning.Should().BeFalse();
    }

    [Fact]
    public void RestingHrAboveRange_IsConcerning()
    {
        var result = HealthStatusCalculator.Evaluate(HealthMetric.RestingHeartRate, 58, [50, 51, 50, 49, 50, 51]);

        result.Status.Should().Be(HealthMetricStatus.AboveRange);
        result.IsConcerning.Should().BeTrue();
    }

    [Fact]
    public void VeryStableHistory_StillGetsMinimumRangeWidth()
    {
        var result = HealthStatusCalculator.Evaluate(HealthMetric.RestingHeartRate, 52, [50, 50, 50, 50, 50, 50]);

        result.RangeLow.Should().Be(48);
        result.RangeHigh.Should().Be(52);
        result.Status.Should().Be(HealthMetricStatus.InRange);
    }

    [Fact]
    public void TooLittleHistory_HasNoRange()
    {
        HealthStatusCalculator.Evaluate(HealthMetric.Hrv, 47, [44, 50, 41]).Status.Should().Be(HealthMetricStatus.NoRange);
        HealthStatusCalculator.Evaluate(HealthMetric.SpO2, null, []).Status.Should().Be(HealthMetricStatus.NoData);
    }
}
