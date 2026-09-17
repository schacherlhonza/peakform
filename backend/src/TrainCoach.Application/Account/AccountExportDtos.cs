using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Account;

/// <summary>
/// Full machine-readable export of everything the app holds about one user (GDPR data
/// portability, security.md §11). Deliberately excludes: raw <c>IntegrationCredential</c> tokens
/// (secrets, never exported even encrypted), internal sync/import bookkeeping
/// (<c>SynchronizationRun</c>, <c>ImportedFile</c>, <c>DataProvenance</c>) which describes how data
/// arrived rather than being personal data itself, and per-sample <c>ActivityMetric</c> time series
/// (exported as a count, not every raw point, to keep the export a reasonable size) — see
/// docs/mvp-scope.md for this documented as an intentional scope decision.
/// </summary>
public record AccountDataExportDto(
    ProfileExportDto Profile,
    IReadOnlyList<RelationshipExportDto> Relationships,
    AthleteDataExportDto? AthleteData,
    IReadOnlyList<CommentExportDto> CommentsAuthored,
    IReadOnlyList<NotificationExportDto> Notifications,
    IReadOnlyList<AuditLogExportDto> AuditLogEntries,
    DateTime ExportedAtUtc);

public record ProfileExportDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    AppRole PrimaryRole,
    string TimeZoneId,
    string Locale,
    string? PhoneNumber,
    bool IsOnboarded,
    DateTime CreatedAtUtc,
    AthleteProfileExportDto? AthleteProfile,
    CoachProfileExportDto? CoachProfile);

public record AthleteProfileExportDto(
    DateOnly? DateOfBirth,
    string? Sex,
    decimal? HeightCm,
    decimal? CurrentWeightKg,
    int? RestingHeartRateBpm,
    int? MaxHeartRateBpm,
    string? PrimarySport,
    string? Notes);

public record CoachProfileExportDto(string? Bio, string? Certifications, int? YearsOfExperience);

public record RelationshipExportDto(
    Guid Id,
    string MyRole,
    Guid CounterpartUserId,
    string CounterpartName,
    RelationshipStatus Status,
    DateTime InvitedAtUtc,
    DateTime? RespondedAtUtc,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? RevokedAtUtc,
    string? RevokedReason,
    IReadOnlyList<PermissionScope> ActiveScopes);

public record AthleteDataExportDto(
    IReadOnlyList<SeasonExportDto> Seasons,
    IReadOnlyList<GoalExportDto> Goals,
    IReadOnlyList<RaceExportDto> Races,
    IReadOnlyList<TrainingPlanExportDto> TrainingPlans,
    IReadOnlyList<CompletedActivityExportDto> CompletedActivities,
    IReadOnlyList<DailyCheckInExportDto> CheckIns,
    IReadOnlyList<PainOrHealthFlagExportDto> HealthFlags,
    IReadOnlyList<SleepRecordExportDto> SleepRecords,
    IReadOnlyList<RecoveryMetricExportDto> RecoveryMetrics,
    IReadOnlyList<HrvMeasurementExportDto> HrvMeasurements,
    IReadOnlyList<PersonalRecordExportDto> PersonalRecords,
    IReadOnlyList<FoodEntryExportDto> FoodEntries,
    IReadOnlyList<HydrationEntryExportDto> HydrationEntries,
    IReadOnlyList<GeneratedReportExportDto> Reports,
    IReadOnlyList<IntegrationConnectionExportDto> IntegrationConnections);

public record SeasonExportDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string? Notes);

public record GoalExportDto(Guid Id, string Title, string? Description, DateOnly? TargetDate, GoalPriority Priority, bool IsAchieved, string? AchievedNotes);

public record RaceExportDto(
    Guid Id, string Name, SportType Sport, DateTime StartsAtUtc, string? Location,
    decimal? DistanceMeters, GoalPriority Priority, int? TargetTimeSeconds, string? TargetResultNote,
    int? ActualTimeSeconds, string? ActualResultNote, string? ResultNotes);

public record TrainingPlanExportDto(
    Guid Id, string Name, DateOnly StartDate, DateOnly? EndDate, bool IsActive, Guid CoachUserId,
    IReadOnlyList<TrainingWeekExportDto> Weeks);

public record TrainingWeekExportDto(
    Guid Id, DateOnly WeekStartDate, int WeekIndex, string? CoachWeekSummary, string? AthleteWeekReflection,
    int? AthleteWeeklyRating, IReadOnlyList<PlannedWorkoutExportDto> Workouts);

public record PlannedWorkoutExportDto(
    Guid Id, DateOnly Date, SportType Sport, string Title, string CoachDescription, bool IsRestDay,
    decimal? PlannedDistanceMeters, int? PlannedDurationSeconds);

public record CompletedActivityExportDto(
    Guid Id, SportType Sport, string? Title, DateTime StartedAtUtc, int DurationSeconds,
    decimal? DistanceMeters, decimal? ElevationGainMeters, int? AverageHeartRateBpm, int? MaxHeartRateBpm,
    int? AveragePaceSecondsPerKm, int? AveragePowerWatts, int? Calories, int AdditionalMetricsCount);

public record DailyCheckInExportDto(
    Guid Id, DateOnly Date, CheckInType Type, WellnessScale? Energy, WellnessScale? Fatigue,
    WellnessScale? Stress, bool? HasPainOrIllness, string? Note, WellnessScale? SleepQuality,
    WellnessScale? MuscleSoreness, WellnessScale? Motivation, decimal? HydrationLiters);

public record PainOrHealthFlagExportDto(
    Guid Id, HealthFlagType Type, HealthFlagSeverity Severity, HealthFlagStatus Status,
    string? BodyPart, string? Description, DateOnly StartedOnDate, DateOnly? ResolvedOnDate);

public record SleepRecordExportDto(Guid Id, DateOnly Date, int? DurationMinutes, int? DeepSleepMinutes, int? RemSleepMinutes, DataSource Source, string? Notes);

public record RecoveryMetricExportDto(Guid Id, DateOnly Date, int? RestingHeartRateBpm, int? ReadinessScore, int? StressScore, DataSource Source);

public record HrvMeasurementExportDto(Guid Id, DateOnly Date, decimal RmssdMs, DataSource Source, string? Notes);

public record PersonalRecordExportDto(Guid Id, SportType Sport, string DistanceLabel, int? TimeSeconds, DateOnly AchievedDate, string? Notes);

public record FoodEntryExportDto(Guid Id, DateTime ConsumedAtUtc, MealType MealType, string Description, decimal? EstimatedCarbsGrams, decimal? EstimatedProteinGrams);

public record HydrationEntryExportDto(Guid Id, DateTime ConsumedAtUtc, HydrationDrinkType DrinkType, decimal VolumeMilliliters, bool ContainsElectrolytes, decimal? CaffeineMilligrams);

public record GeneratedReportExportDto(Guid Id, DateOnly Date, ReportType Type, DateTime GeneratedAtUtc, string NarrativeText, IReadOnlyList<string> InsightMessages);

public record IntegrationConnectionExportDto(Guid Id, IntegrationProviderType Provider, IntegrationConnectionStatus Status, string? ExternalAccountId, DateTime? ConnectedAtUtc, DateTime? LastSyncedAtUtc, DateTime? DisconnectedAtUtc);

public record CommentExportDto(Guid Id, Guid PlannedWorkoutId, CommentAuthorRole AuthorRole, string Text, DateTime CreatedAtUtc);

public record NotificationExportDto(Guid Id, NotificationType Type, string Title, string? Body, DateTime CreatedAtUtc, DateTime? ReadAtUtc);

public record AuditLogExportDto(Guid Id, AuditAction Action, string EntityName, Guid? EntityId, string? Details, DateTime OccurredAtUtc);
