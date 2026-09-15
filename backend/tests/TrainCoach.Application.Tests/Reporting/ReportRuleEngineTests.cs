using FluentAssertions;
using TrainCoach.Application.Reporting;
using TrainCoach.Domain.Enums;
using Xunit;

namespace TrainCoach.Application.Tests.Reporting;

public class ReportRuleEngineTests
{
    private readonly ReportRuleEngine _sut = new();

    private static ReportInputSnapshot BaseSnapshot(ReportType type = ReportType.Morning) => new(
        AthleteUserId: Guid.NewGuid(),
        Date: new DateOnly(2026, 1, 5),
        Type: type,
        AthleteFirstName: "Petra",
        Energy: null, Fatigue: null, LegsFeeling: null, Stress: null, SleepQuality: null,
        HasPainOrIllness: null, CompletedPlannedWorkout: null, Rpe: null,
        RestingHeartRateBpm: null, BaselineRestingHeartRate: null, BaselineRestingHeartRateStdDev: null,
        HrvRmssdMs: null, BaselineHrv: null, BaselineHrvStdDev: null, SleepDurationMinutes: null,
        HasActiveModerateOrSevereHealthFlag: false, IsRestDay: true, TodayWorkoutTitle: null,
        SevenDayDistanceMeters: 0, DaysUntilNextRace: null, NextRaceName: null);

    [Fact]
    public void Evaluate_WithNothingUnusual_ReturnsNoInsights()
    {
        var result = _sut.Evaluate(BaseSnapshot());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_RestingHeartRateWellAboveBaseline_FlagsAttention()
    {
        var snapshot = BaseSnapshot() with
        {
            RestingHeartRateBpm = 65,
            BaselineRestingHeartRate = 50,
            BaselineRestingHeartRateStdDev = 3,
        };

        var result = _sut.Evaluate(snapshot);

        result.Should().ContainSingle(i => i.RuleCode == "resting_hr_elevated" && i.Severity == InsightSeverity.Attention);
    }

    [Fact]
    public void Evaluate_RestingHeartRateWithinNormalRange_DoesNotFlag()
    {
        var snapshot = BaseSnapshot() with
        {
            RestingHeartRateBpm = 51,
            BaselineRestingHeartRate = 50,
            BaselineRestingHeartRateStdDev = 3,
        };

        var result = _sut.Evaluate(snapshot);

        result.Should().NotContain(i => i.RuleCode == "resting_hr_elevated");
    }

    [Fact]
    public void Evaluate_HrvWellBelowBaseline_FlagsAttention()
    {
        var snapshot = BaseSnapshot() with
        {
            HrvRmssdMs = 30,
            BaselineHrv = 55,
            BaselineHrvStdDev = 5,
        };

        var result = _sut.Evaluate(snapshot);

        result.Should().ContainSingle(i => i.RuleCode == "hrv_dropped" && i.Severity == InsightSeverity.Attention);
    }

    [Fact]
    public void Evaluate_ActiveHealthFlag_FlagsAttention_WithNoMedicalLanguageAsDiagnosis()
    {
        var snapshot = BaseSnapshot() with { HasActiveModerateOrSevereHealthFlag = true };

        var result = _sut.Evaluate(snapshot);

        var insight = result.Should().ContainSingle(i => i.RuleCode == "active_health_flag").Subject;
        insight.Severity.Should().Be(InsightSeverity.Attention);
        insight.Message.Should().Contain("NENÍ lékařské doporučení");
    }

    [Fact]
    public void Evaluate_RaceApproaching_OnlyFiresForMorningReport()
    {
        var morning = BaseSnapshot(ReportType.Morning) with { DaysUntilNextRace = 2, NextRaceName = "Jarní půlmaraton" };
        var evening = BaseSnapshot(ReportType.Evening) with { DaysUntilNextRace = 2, NextRaceName = "Jarní půlmaraton" };

        _sut.Evaluate(morning).Should().Contain(i => i.RuleCode == "race_approaching");
        _sut.Evaluate(evening).Should().NotContain(i => i.RuleCode == "race_approaching");
    }

    [Fact]
    public void Evaluate_MissedWorkout_OnlyFiresForEveningReport_AndNotOnRestDay()
    {
        var evening = BaseSnapshot(ReportType.Evening) with { IsRestDay = false, CompletedPlannedWorkout = false };
        var eveningRestDay = BaseSnapshot(ReportType.Evening) with { IsRestDay = true, CompletedPlannedWorkout = false };

        _sut.Evaluate(evening).Should().Contain(i => i.RuleCode == "missed_planned_workout");
        _sut.Evaluate(eveningRestDay).Should().NotContain(i => i.RuleCode == "missed_planned_workout");
    }

    [Fact]
    public void Evaluate_OrdersInsightsMostSevereFirst()
    {
        var snapshot = BaseSnapshot(ReportType.Morning) with
        {
            SleepQuality = WellnessScale.Poor,
            HasActiveModerateOrSevereHealthFlag = true,
            DaysUntilNextRace = 1,
            NextRaceName = "Testovací závod",
        };

        var result = _sut.Evaluate(snapshot);

        result.Should().HaveCountGreaterThan(1);
        result.First().Severity.Should().Be(InsightSeverity.Attention);
        result.Last().Severity.Should().BeOneOf(InsightSeverity.Info, InsightSeverity.Notice);
    }
}
