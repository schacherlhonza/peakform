using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Identity;

namespace TrainCoach.Application.Account;

public class AccountExportService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    IDateTimeProvider clock,
    IAuditLogService auditLog) : IAccountExportService
{
    public async Task<AccountDataExportDto> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await db.UserProfiles
            .Include(p => p.AthleteProfile)
            .Include(p => p.CoachProfile)
            .FirstOrDefaultAsync(p => p.Id == userId, cancellationToken)
            ?? throw new NotFoundException("UserProfile", userId);

        var email = (await userLookup.GetEmailsAsync([userId], cancellationToken)).GetValueOrDefault(userId, string.Empty);

        var profileDto = new ProfileExportDto(
            profile.Id, email, profile.FirstName, profile.LastName, profile.PrimaryRole,
            profile.TimeZoneId, profile.Locale, profile.PhoneNumber, profile.IsOnboarded, profile.CreatedAtUtc,
            profile.AthleteProfile is null ? null : new AthleteProfileExportDto(
                profile.AthleteProfile.DateOfBirth, profile.AthleteProfile.Sex, profile.AthleteProfile.HeightCm,
                profile.AthleteProfile.CurrentWeightKg, profile.AthleteProfile.RestingHeartRateBpm,
                profile.AthleteProfile.MaxHeartRateBpm, profile.AthleteProfile.PrimarySport, profile.AthleteProfile.Notes),
            profile.CoachProfile is null ? null : new CoachProfileExportDto(
                profile.CoachProfile.Bio, profile.CoachProfile.Certifications, profile.CoachProfile.YearsOfExperience));

        var relationships = await db.CoachAthleteRelationships
            .Include(r => r.Permissions)
            .Where(r => r.CoachUserId == userId || r.AthleteUserId == userId)
            .ToListAsync(cancellationToken);

        var counterpartIds = relationships.Select(r => r.CoachUserId == userId ? r.AthleteUserId : r.CoachUserId).Distinct().ToList();
        var counterpartNames = await db.UserProfiles
            .Where(p => counterpartIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        var relationshipDtos = relationships.Select(r =>
        {
            var isCoach = r.CoachUserId == userId;
            var counterpartId = isCoach ? r.AthleteUserId : r.CoachUserId;
            return new RelationshipExportDto(
                r.Id, isCoach ? "Coach" : "Athlete", counterpartId, counterpartNames.GetValueOrDefault(counterpartId, "?"),
                r.Status, r.InvitedAtUtc, r.RespondedAtUtc, r.StartDateUtc, r.EndDateUtc, r.RevokedAtUtc, r.RevokedReason,
                r.Permissions.Where(p => p.RevokedAtUtc == null).Select(p => p.Scope).ToList());
        }).ToList();

        var athleteData = profile.PrimaryRole == AppRole.Athlete ? await ExportAthleteDataAsync(userId, cancellationToken) : null;

        var commentsAuthored = await db.Comments
            .Where(c => c.AuthorUserId == userId)
            .Select(c => new CommentExportDto(c.Id, c.PlannedWorkoutId, c.AuthorRole, c.Text, c.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .Select(n => new NotificationExportDto(n.Id, n.Type, n.Title, n.Body, n.CreatedAtUtc, n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        var auditLogEntries = await db.AuditLogs
            .Where(a => a.ActorUserId == userId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Select(a => new AuditLogExportDto(a.Id, a.Action, a.EntityName, a.EntityId, a.Details, a.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        auditLog.Record(userId, AuditAction.DataExported, nameof(UserProfile), userId);
        await db.SaveChangesAsync(cancellationToken);

        return new AccountDataExportDto(profileDto, relationshipDtos, athleteData, commentsAuthored, notifications, auditLogEntries, clock.UtcNow);
    }

    private async Task<AthleteDataExportDto> ExportAthleteDataAsync(Guid athleteUserId, CancellationToken cancellationToken)
    {
        var seasons = await db.Seasons.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new SeasonExportDto(x.Id, x.Name, x.StartDate, x.EndDate, x.Notes))
            .ToListAsync(cancellationToken);

        var goals = await db.Goals.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new GoalExportDto(x.Id, x.Title, x.Description, x.TargetDate, x.Priority, x.IsAchieved, x.AchievedNotes))
            .ToListAsync(cancellationToken);

        var races = await db.Races.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new RaceExportDto(x.Id, x.Name, x.Sport, x.StartsAtUtc, x.Location, x.DistanceMeters, x.Priority,
                x.TargetTimeSeconds, x.TargetResultNote, x.ActualTimeSeconds, x.ActualResultNote, x.ResultNotes))
            .ToListAsync(cancellationToken);

        var plans = await db.TrainingPlans
            .Include(p => p.Weeks).ThenInclude(w => w.Workouts)
            .Where(x => x.AthleteUserId == athleteUserId)
            .ToListAsync(cancellationToken);
        var planDtos = plans.Select(p => new TrainingPlanExportDto(
            p.Id, p.Name, p.StartDate, p.EndDate, p.IsActive, p.CoachUserId,
            p.Weeks.Select(w => new TrainingWeekExportDto(
                w.Id, w.WeekStartDate, w.WeekIndex, w.CoachWeekSummary, w.AthleteWeekReflection, w.AthleteWeeklyRating,
                w.Workouts.Select(pw => new PlannedWorkoutExportDto(
                    pw.Id, pw.Date, pw.Sport, pw.Title, pw.CoachDescription, pw.IsRestDay,
                    pw.PlannedDistanceMeters, pw.PlannedDurationSeconds)).ToList())).ToList())).ToList();

        var activities = await db.CompletedActivities
            .Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new CompletedActivityExportDto(
                x.Id, x.Sport, x.Title, x.StartedAtUtc, x.DurationSeconds, x.DistanceMeters, x.ElevationGainMeters,
                x.AverageHeartRateBpm, x.MaxHeartRateBpm, x.AveragePaceSecondsPerKm, x.AveragePowerWatts, x.Calories,
                x.AdditionalMetrics.Count))
            .ToListAsync(cancellationToken);

        var checkIns = await db.DailyCheckIns.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new DailyCheckInExportDto(x.Id, x.Date, x.Type, x.Energy, x.Fatigue, x.Stress, x.HasPainOrIllness,
                x.Note, x.SleepQuality, x.MuscleSoreness, x.Motivation, x.HydrationLiters))
            .ToListAsync(cancellationToken);

        var healthFlags = await db.PainOrHealthFlags.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new PainOrHealthFlagExportDto(x.Id, x.Type, x.Severity, x.Status, x.BodyPart, x.Description, x.StartedOnDate, x.ResolvedOnDate))
            .ToListAsync(cancellationToken);

        var sleepRecords = await db.SleepRecords.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new SleepRecordExportDto(x.Id, x.Date, x.DurationMinutes, x.DeepSleepMinutes, x.RemSleepMinutes, x.Source, x.Notes))
            .ToListAsync(cancellationToken);

        var recoveryMetrics = await db.RecoveryMetrics.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new RecoveryMetricExportDto(x.Id, x.Date, x.RestingHeartRateBpm, x.ReadinessScore, x.StressScore, x.Source))
            .ToListAsync(cancellationToken);

        var hrvMeasurements = await db.HrvMeasurements.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new HrvMeasurementExportDto(x.Id, x.Date, x.RmssdMs, x.Source, x.Notes))
            .ToListAsync(cancellationToken);

        var personalRecords = await db.PersonalRecords.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new PersonalRecordExportDto(x.Id, x.Sport, x.DistanceLabel, x.TimeSeconds, x.AchievedDate, x.Notes))
            .ToListAsync(cancellationToken);

        var foodEntries = await db.FoodEntries.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new FoodEntryExportDto(x.Id, x.ConsumedAtUtc, x.MealType, x.Description, x.EstimatedCarbsGrams, x.EstimatedProteinGrams))
            .ToListAsync(cancellationToken);

        var hydrationEntries = await db.HydrationEntries.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new HydrationEntryExportDto(x.Id, x.ConsumedAtUtc, x.DrinkType, x.VolumeMilliliters, x.ContainsElectrolytes, x.CaffeineMilligrams))
            .ToListAsync(cancellationToken);

        var reports = await db.GeneratedReports
            .Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new GeneratedReportExportDto(x.Id, x.Date, x.Type, x.GeneratedAtUtc, x.NarrativeText, x.Insights.Select(i => i.Message).ToList()))
            .ToListAsync(cancellationToken);

        var integrationConnections = await db.IntegrationConnections.Where(x => x.AthleteUserId == athleteUserId)
            .Select(x => new IntegrationConnectionExportDto(x.Id, x.Provider, x.Status, x.ExternalAccountId, x.ConnectedAtUtc, x.LastSyncedAtUtc, x.DisconnectedAtUtc))
            .ToListAsync(cancellationToken);

        return new AthleteDataExportDto(
            seasons, goals, races, planDtos, activities, checkIns, healthFlags, sleepRecords, recoveryMetrics,
            hrvMeasurements, personalRecords, foodEntries, hydrationEntries, reports, integrationConnections);
    }
}
