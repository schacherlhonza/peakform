using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Reporting;

public class ReportMetricsCalculator(IApplicationDbContext db) : IReportMetricsCalculator
{
    public async Task<ReportInputSnapshot> CalculateAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default)
    {
        var firstName = await db.UserProfiles
            .Where(p => p.Id == athleteUserId)
            .Select(p => p.FirstName)
            .FirstOrDefaultAsync(cancellationToken) ?? "sportovče";

        var checkInType = type == ReportType.Morning ? CheckInType.Evening : CheckInType.Morning;
        // Evening report reflects on the day using the morning check-in + evening check-in fields
        // already captured today; morning report looks at yesterday evening's check-in.
        var checkIn = type == ReportType.Morning
            ? await db.DailyCheckIns.FirstOrDefaultAsync(
                c => c.AthleteUserId == athleteUserId && c.Date == date.AddDays(-1) && c.Type == CheckInType.Evening, cancellationToken)
            : await db.DailyCheckIns.FirstOrDefaultAsync(
                c => c.AthleteUserId == athleteUserId && c.Date == date, cancellationToken);

        var morningCheckIn = await db.DailyCheckIns.FirstOrDefaultAsync(
            c => c.AthleteUserId == athleteUserId && c.Date == date && c.Type == CheckInType.Morning, cancellationToken);

        var recovery = await db.RecoveryMetrics
            .Where(r => r.AthleteUserId == athleteUserId && r.Date == date)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var hrv = await db.HrvMeasurements
            .Where(h => h.AthleteUserId == athleteUserId && h.Date == date)
            .OrderByDescending(h => h.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var sleep = await db.SleepRecords
            .Where(s => s.AthleteUserId == athleteUserId && s.Date == date)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var baselineHr = await LatestBaselineAsync(db, athleteUserId, BaselineMetricType.RestingHeartRate, date, cancellationToken);
        var baselineHrv = await LatestBaselineAsync(db, athleteUserId, BaselineMetricType.Hrv, date, cancellationToken);

        var hasActiveFlag = await db.PainOrHealthFlags.AnyAsync(
            f => f.AthleteUserId == athleteUserId
                 && f.Status != HealthFlagStatus.Resolved
                 && f.Severity >= HealthFlagSeverity.Moderate,
            cancellationToken);

        var todayWorkout = await db.PlannedWorkouts
            .Where(w => w.Date == date && w.TrainingWeek.TrainingPlan.AthleteUserId == athleteUserId)
            .OrderByDescending(w => w.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var sevenDaysAgo = date.AddDays(-7).ToDateTime(TimeOnly.MinValue);
        var todayEnd = date.ToDateTime(TimeOnly.MaxValue);
        var sevenDayDistance = await db.CompletedActivities
            .Where(a => a.AthleteUserId == athleteUserId && a.StartedAtUtc >= sevenDaysAgo && a.StartedAtUtc <= todayEnd)
            .SumAsync(a => a.DistanceMeters ?? 0, cancellationToken);

        var nowUtc = date.ToDateTime(TimeOnly.MinValue);
        var nextRace = await db.Races
            .Where(r => r.AthleteUserId == athleteUserId && r.StartsAtUtc >= nowUtc)
            .OrderBy(r => r.StartsAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new ReportInputSnapshot(
            athleteUserId,
            date,
            type,
            firstName,
            Energy: checkIn?.Energy,
            Fatigue: checkIn?.Fatigue,
            LegsFeeling: checkIn?.LegsFeeling,
            Stress: checkIn?.Stress,
            SleepQuality: morningCheckIn?.SleepQuality,
            HasPainOrIllness: checkIn?.HasPainOrIllness ?? morningCheckIn?.HasPainOrIllness,
            CompletedPlannedWorkout: checkIn?.CompletedPlannedWorkout,
            Rpe: checkIn?.Rpe,
            RestingHeartRateBpm: recovery?.RestingHeartRateBpm,
            BaselineRestingHeartRate: baselineHr?.BaselineValue,
            BaselineRestingHeartRateStdDev: baselineHr?.StdDeviation,
            HrvRmssdMs: hrv?.RmssdMs,
            BaselineHrv: baselineHrv?.BaselineValue,
            BaselineHrvStdDev: baselineHrv?.StdDeviation,
            SleepDurationMinutes: sleep?.DurationMinutes,
            HasActiveModerateOrSevereHealthFlag: hasActiveFlag,
            IsRestDay: todayWorkout?.IsRestDay ?? true,
            TodayWorkoutTitle: todayWorkout?.Title,
            SevenDayDistanceMeters: sevenDayDistance,
            DaysUntilNextRace: nextRace is null ? null : (int)(nextRace.StartsAtUtc.Date - nowUtc.Date).TotalDays,
            NextRaceName: nextRace?.Name);
    }

    private static async Task<Domain.Wellness.PerformanceBaseline?> LatestBaselineAsync(
        IApplicationDbContext db, Guid athleteUserId, BaselineMetricType metricType, DateOnly date, CancellationToken cancellationToken)
    {
        return await db.PerformanceBaselines
            .Where(b => b.AthleteUserId == athleteUserId && b.MetricType == metricType && b.ValidFromDate <= date)
            .OrderByDescending(b => b.ValidFromDate)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
