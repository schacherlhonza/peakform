using Microsoft.EntityFrameworkCore;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Identity;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Nutrition;
using TrainCoach.Domain.Planning;
using TrainCoach.Domain.Platform;
using TrainCoach.Domain.Reporting;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Common;

/// <summary>
/// The single query/persistence seam the Application layer depends on. Deliberately not a
/// repository-per-aggregate: with ~30 entity types a repository layer would just re-expose EF
/// Core's own querying with extra ceremony. Implemented by TrainCoach.Infrastructure's
/// TrainCoachDbContext, so Application never references EF Core provider packages.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<AthleteProfile> AthleteProfiles { get; }
    DbSet<CoachProfile> CoachProfiles { get; }
    DbSet<CoachAthleteRelationship> CoachAthleteRelationships { get; }
    DbSet<RelationshipPermission> RelationshipPermissions { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Season> Seasons { get; }
    DbSet<Goal> Goals { get; }
    DbSet<Race> Races { get; }
    DbSet<TrainingPlan> TrainingPlans { get; }
    DbSet<TrainingWeek> TrainingWeeks { get; }
    DbSet<PlannedWorkout> PlannedWorkouts { get; }
    DbSet<WorkoutTemplate> WorkoutTemplates { get; }
    DbSet<WorkoutSegment> WorkoutSegments { get; }
    DbSet<HeartRateZone> HeartRateZones { get; }
    DbSet<CustomAbbreviation> CustomAbbreviations { get; }

    DbSet<CompletedActivity> CompletedActivities { get; }
    DbSet<ActivityMetric> ActivityMetrics { get; }
    DbSet<DataProvenance> DataProvenances { get; }
    DbSet<TrainingFeedback> TrainingFeedbacks { get; }
    DbSet<Comment> Comments { get; }

    DbSet<DailyCheckIn> DailyCheckIns { get; }
    DbSet<PainOrHealthFlag> PainOrHealthFlags { get; }
    DbSet<SleepRecord> SleepRecords { get; }
    DbSet<RecoveryMetric> RecoveryMetrics { get; }
    DbSet<HrvMeasurement> HrvMeasurements { get; }
    DbSet<PerformanceBaseline> PerformanceBaselines { get; }
    DbSet<PersonalRecord> PersonalRecords { get; }

    DbSet<FoodEntry> FoodEntries { get; }
    DbSet<HydrationEntry> HydrationEntries { get; }

    DbSet<GeneratedReport> GeneratedReports { get; }
    DbSet<GeneratedReportInsight> GeneratedReportInsights { get; }

    DbSet<IntegrationConnection> IntegrationConnections { get; }
    DbSet<IntegrationCredential> IntegrationCredentials { get; }
    DbSet<SynchronizationRun> SynchronizationRuns { get; }
    DbSet<ImportedFile> ImportedFiles { get; }

    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
