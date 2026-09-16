using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Identity;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Nutrition;
using TrainCoach.Domain.Planning;
using TrainCoach.Domain.Platform;
using TrainCoach.Domain.Reporting;
using TrainCoach.Domain.Wellness;
using TrainCoach.Infrastructure.Identity;

namespace TrainCoach.Infrastructure.Persistence;

public class TrainCoachDbContext(DbContextOptions<TrainCoachDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    // Identity / access
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AthleteProfile> AthleteProfiles => Set<AthleteProfile>();
    public DbSet<CoachProfile> CoachProfiles => Set<CoachProfile>();
    public DbSet<CoachAthleteRelationship> CoachAthleteRelationships => Set<CoachAthleteRelationship>();
    public DbSet<RelationshipPermission> RelationshipPermissions => Set<RelationshipPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Planning
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<TrainingWeek> TrainingWeeks => Set<TrainingWeek>();
    public DbSet<PlannedWorkout> PlannedWorkouts => Set<PlannedWorkout>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<WorkoutSegment> WorkoutSegments => Set<WorkoutSegment>();
    public DbSet<HeartRateZone> HeartRateZones => Set<HeartRateZone>();
    public DbSet<CustomAbbreviation> CustomAbbreviations => Set<CustomAbbreviation>();

    // Execution & feedback
    public DbSet<CompletedActivity> CompletedActivities => Set<CompletedActivity>();
    public DbSet<ActivityMetric> ActivityMetrics => Set<ActivityMetric>();
    public DbSet<DataProvenance> DataProvenances => Set<DataProvenance>();
    public DbSet<TrainingFeedback> TrainingFeedbacks => Set<TrainingFeedback>();
    public DbSet<Comment> Comments => Set<Comment>();

    // Wellness
    public DbSet<DailyCheckIn> DailyCheckIns => Set<DailyCheckIn>();
    public DbSet<PainOrHealthFlag> PainOrHealthFlags => Set<PainOrHealthFlag>();
    public DbSet<SleepRecord> SleepRecords => Set<SleepRecord>();
    public DbSet<RecoveryMetric> RecoveryMetrics => Set<RecoveryMetric>();
    public DbSet<HrvMeasurement> HrvMeasurements => Set<HrvMeasurement>();
    public DbSet<PerformanceBaseline> PerformanceBaselines => Set<PerformanceBaseline>();
    public DbSet<PersonalRecord> PersonalRecords => Set<PersonalRecord>();

    // Nutrition
    public DbSet<FoodEntry> FoodEntries => Set<FoodEntry>();
    public DbSet<HydrationEntry> HydrationEntries => Set<HydrationEntry>();

    // Reporting
    public DbSet<GeneratedReport> GeneratedReports => Set<GeneratedReport>();
    public DbSet<GeneratedReportInsight> GeneratedReportInsights => Set<GeneratedReportInsight>();

    // Integrations
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationCredential> IntegrationCredentials => Set<IntegrationCredential>();
    public DbSet<SynchronizationRun> SynchronizationRuns => Set<SynchronizationRun>();
    public DbSet<ImportedFile> ImportedFiles => Set<ImportedFile>();

    // Platform
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Blanket precision for all decimal columns (distances, paces, macros, ...) so the
        // model never relies on provider-specific default numeric precision.
        configurationBuilder.Properties<decimal>().HavePrecision(12, 3);

        // Every DateTime property is a UTC instant; stamp Kind=Utc uniformly so Npgsql's
        // "timestamp with time zone" columns accept values from DateOnly.ToDateTime(...) etc.
        // without every call site having to remember DateTime.SpecifyKind.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(TrainCoachDbContext).Assembly);

        // Identity tables use the framework's default "AspNetXxx" names; nothing to rename.
    }
}
