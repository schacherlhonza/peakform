using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Reporting;

/// <summary>
/// Deliberately one class with one small, named rule method each — not a plugin/strategy
/// framework. With ~10 rules that would be ceremony without payoff; keeping them as private
/// methods on one reviewable class still gives each rule its own RuleCode, its own test, and
/// a single place to read "everything the system can flag and why".
/// </summary>
public class ReportRuleEngine : IReportRuleEngine
{
    public IReadOnlyList<GeneratedReportInsightDto> Evaluate(ReportInputSnapshot s)
    {
        var insights = new List<GeneratedReportInsightDto?>
        {
            RestingHeartRateElevated(s),
            HrvDropped(s),
            ActiveHealthFlag(s),
            PoorSleep(s),
            HighFatigue(s),
            ElevatedStress(s),
            RaceApproaching(s),
            MissedPlannedWorkout(s),
        };

        return insights.Where(i => i is not null).Select(i => i!)
            .OrderByDescending(i => i.Severity)
            .ToList();
    }

    private static GeneratedReportInsightDto? RestingHeartRateElevated(ReportInputSnapshot s)
    {
        if (s.RestingHeartRateBpm is null || s.BaselineRestingHeartRate is null || s.BaselineRestingHeartRateStdDev is null)
        {
            return null;
        }

        var threshold = s.BaselineRestingHeartRate.Value + 1.5m * s.BaselineRestingHeartRateStdDev.Value;
        if (s.RestingHeartRateBpm.Value <= threshold)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "resting_hr_elevated",
            InsightSeverity.Attention,
            $"Klidová tepová frekvence ({s.RestingHeartRateBpm} tepů/min) je dnes výrazně nad tvým obvyklým průměrem ({s.BaselineRestingHeartRate:0} tepů/min). Může jít o signál nedostatečné regenerace.");
    }

    private static GeneratedReportInsightDto? HrvDropped(ReportInputSnapshot s)
    {
        if (s.HrvRmssdMs is null || s.BaselineHrv is null || s.BaselineHrvStdDev is null)
        {
            return null;
        }

        var threshold = s.BaselineHrv.Value - 1.5m * s.BaselineHrvStdDev.Value;
        if (s.HrvRmssdMs.Value >= threshold)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "hrv_dropped",
            InsightSeverity.Attention,
            $"Tvoje HRV ({s.HrvRmssdMs:0.#} ms) je dnes pod obvyklým pásmem ({s.BaselineHrv:0.#} ms). Zvaž lehčí zátěž a více odpočinku.");
    }

    private static GeneratedReportInsightDto? ActiveHealthFlag(ReportInputSnapshot s)
    {
        if (!s.HasActiveModerateOrSevereHealthFlag)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "active_health_flag",
            InsightSeverity.Attention,
            "Máš aktivní hlášenou bolest nebo nemoc. Toto NENÍ lékařské doporučení — v případě pochybností konzultuj zdravotníka.");
    }

    private static GeneratedReportInsightDto? PoorSleep(ReportInputSnapshot s)
    {
        if (s.SleepQuality is null || s.SleepQuality > WellnessScale.Poor)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "poor_sleep",
            InsightSeverity.Notice,
            "Hlásíš horší kvalitu spánku. Dnešní den zvol podle toho, jak se cítíš.");
    }

    private static GeneratedReportInsightDto? HighFatigue(ReportInputSnapshot s)
    {
        if (s.Fatigue is null || s.Fatigue > WellnessScale.Poor)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "high_fatigue",
            InsightSeverity.Notice,
            "Hlásíš vyšší únavu. Sleduj, jak na dnešní plán reaguje tělo.");
    }

    private static GeneratedReportInsightDto? ElevatedStress(ReportInputSnapshot s)
    {
        if (s.Stress is null || s.Stress > WellnessScale.Poor)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "elevated_stress",
            InsightSeverity.Notice,
            "Hlásíš zvýšený stres. I mimosportovní zátěž ovlivňuje regeneraci.");
    }

    private static GeneratedReportInsightDto? RaceApproaching(ReportInputSnapshot s)
    {
        if (s.Type != ReportType.Morning || s.DaysUntilNextRace is not (>= 0 and <= 3) || s.NextRaceName is null)
        {
            return null;
        }

        var days = s.DaysUntilNextRace.Value;
        var when = days == 0 ? "dnes" : days == 1 ? "zítra" : $"za {days} dny";

        return new GeneratedReportInsightDto(
            "race_approaching",
            InsightSeverity.Info,
            $"Závod \"{s.NextRaceName}\" tě čeká {when}.");
    }

    private static GeneratedReportInsightDto? MissedPlannedWorkout(ReportInputSnapshot s)
    {
        if (s.Type != ReportType.Evening || s.IsRestDay || s.CompletedPlannedWorkout != false)
        {
            return null;
        }

        return new GeneratedReportInsightDto(
            "missed_planned_workout",
            InsightSeverity.Notice,
            "Dnešní plánovaný trénink podle check-inu neproběhl podle plánu — dej trenérovi vědět, co se stalo.");
    }
}
