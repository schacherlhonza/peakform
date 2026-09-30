using FluentAssertions;
using TrainCoach.Application.Wellness;
using TrainCoach.Domain.Enums;
using Xunit;

namespace TrainCoach.Application.Tests.Wellness;

public class ReadinessCalculatorTests
{
    private static ReadinessInputs Inputs(
        decimal? hrv = null, decimal? hrvBaseline = null, int? rhr = null, decimal? rhrBaseline = null,
        int? sleepMinutes = null, int? sleepScore = null, decimal? ctl = null, decimal? atl = null,
        WellnessScale[]? subjective = null, decimal? vendor = null) =>
        new(hrv, hrvBaseline, rhr, rhrBaseline, sleepMinutes, sleepScore, ctl, atl, subjective ?? [], vendor);

    [Fact]
    public void GarminViaIntervalsIcu_WithoutVendorScore_ComputesWeightedEstimate()
    {
        // Real shape of the data that left the dashboard card empty: HRV/RHR/sleep/load, no readiness.
        var result = ReadinessCalculator.Calculate(Inputs(
            hrv: 43, hrvBaseline: 47.7m, rhr: 52, rhrBaseline: 51.5m, sleepMinutes: 515, sleepScore: 86, ctl: 30.3m, atl: 65.1m));

        result.Source.Should().Be(ReadinessScoreSource.Computed);
        result.Components.Select(c => c.Factor).Should().BeEquivalentTo(
            [ReadinessFactor.Hrv, ReadinessFactor.RestingHeartRate, ReadinessFactor.Sleep, ReadinessFactor.TrainingLoad]);
        result.Components.Single(c => c.Factor == ReadinessFactor.Hrv).SubScore.Should().Be(50);
        result.Components.Single(c => c.Factor == ReadinessFactor.TrainingLoad).SubScore.Should().Be(28);
        result.Score.Should().BeInRange(55, 70);
    }

    [Fact]
    public void AtBaselineWellRestedAndFresh_ScoresHigh()
    {
        var result = ReadinessCalculator.Calculate(Inputs(hrv: 60, hrvBaseline: 58, rhr: 48, rhrBaseline: 49, sleepScore: 90, ctl: 50, atl: 45));

        result.Score.Should().BeGreaterThanOrEqualTo(85);
    }

    [Fact]
    public void VendorScore_WinsOverEstimate_ButComponentsStayForExplanation()
    {
        var result = ReadinessCalculator.Calculate(Inputs(hrv: 43, hrvBaseline: 47.7m, sleepScore: 86, vendor: 71));

        result.Score.Should().Be(71);
        result.Source.Should().Be(ReadinessScoreSource.Vendor);
        result.Components.Should().HaveCount(2);
    }

    [Fact]
    public void SingleSignal_IsTooThinForAScore()
    {
        var result = ReadinessCalculator.Calculate(Inputs(sleepMinutes: 420));

        result.Score.Should().BeNull();
        result.Components.Should().ContainSingle(c => c.Factor == ReadinessFactor.Sleep);
    }

    [Fact]
    public void HrvWithoutBaseline_IsNotScored()
    {
        var result = ReadinessCalculator.Calculate(Inputs(hrv: 43, sleepScore: 80));

        result.Components.Should().NotContain(c => c.Factor == ReadinessFactor.Hrv);
        result.Score.Should().BeNull();
    }

    [Fact]
    public void MorningCheckIn_ContributesSubjectiveComponent()
    {
        var result = ReadinessCalculator.Calculate(Inputs(sleepScore: 80, subjective: [WellnessScale.Good, WellnessScale.VeryGood]));

        result.Components.Single(c => c.Factor == ReadinessFactor.Subjective).SubScore.Should().Be(88);
        result.Score.Should().NotBeNull();
    }
}
