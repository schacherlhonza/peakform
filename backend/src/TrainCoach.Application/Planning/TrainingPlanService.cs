using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Planning;

public class TrainingPlanService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock) : ITrainingPlanService
{
    public async Task<IReadOnlyList<TrainingPlanDto>> GetPlansForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        var plans = await db.TrainingPlans
            .Where(p => p.AthleteUserId == athleteUserId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        return plans.Select(ToDto).ToList();
    }

    public async Task<TrainingPlanDetailDto> GetPlanDetailAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await db.TrainingPlans
            .Include(p => p.Weeks).ThenInclude(w => w.Workouts).ThenInclude(w => w.Segments)
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new NotFoundException("TrainingPlan", planId);

        await accessGuard.EnsureAthleteAccessAsync(plan.AthleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        return new TrainingPlanDetailDto(
            plan.Id, plan.AthleteUserId, plan.CoachUserId, plan.SeasonId, plan.Name, plan.StartDate, plan.EndDate, plan.IsActive,
            plan.Weeks.OrderBy(w => w.WeekStartDate).Select(ToWeekDto).ToList());
    }

    public async Task<TrainingPlanDto> CreatePlanAsync(Guid coachUserId, CreateTrainingPlanRequest request, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(request.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var plan = new TrainingPlan
        {
            AthleteUserId = request.AthleteUserId,
            CoachUserId = coachUserId,
            SeasonId = request.SeasonId,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedAtUtc = clock.UtcNow,
            CreatedByUserId = coachUserId,
        };
        db.TrainingPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task<TrainingWeekDto> CreateWeekAsync(Guid planId, CreateTrainingWeekRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await db.TrainingPlans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new NotFoundException("TrainingPlan", planId);

        await accessGuard.EnsureAthleteAccessAsync(plan.AthleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var week = new TrainingWeek
        {
            TrainingPlanId = planId,
            WeekStartDate = request.WeekStartDate,
            WeekIndex = request.WeekIndex,
            CreatedAtUtc = clock.UtcNow,
        };
        db.TrainingWeeks.Add(week);
        await db.SaveChangesAsync(cancellationToken);
        return ToWeekDto(week);
    }

    public async Task<TrainingWeekDto> UpdateWeekAsync(Guid weekId, AppRole callerRole, UpdateTrainingWeekRequest request, CancellationToken cancellationToken = default)
    {
        var week = await db.TrainingWeeks.Include(w => w.Workouts).ThenInclude(w => w.Segments)
            .FirstOrDefaultAsync(w => w.Id == weekId, cancellationToken)
            ?? throw new NotFoundException("TrainingWeek", weekId);

        var athleteUserId = await db.TrainingPlans.Where(p => p.Id == week.TrainingPlanId).Select(p => p.AthleteUserId).FirstAsync(cancellationToken);

        // Both sides can annotate a week, but each only owns their own fields.
        var requiredScope = callerRole == AppRole.Coach ? PermissionScope.EditTrainingPlan : (PermissionScope?)null;
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, requiredScope, cancellationToken);

        if (callerRole == AppRole.Coach)
        {
            week.CoachWeekSummary = request.CoachWeekSummary;
        }
        else
        {
            week.AthleteWeekReflection = request.AthleteWeekReflection;
            week.WhatWasMissingNote = request.WhatWasMissingNote;
            week.AthleteWeeklyRating = request.AthleteWeeklyRating;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToWeekDto(week);
    }

    public async Task<PlannedWorkoutDto> GetWorkoutAsync(Guid workoutId, CancellationToken cancellationToken = default)
    {
        var workout = await db.PlannedWorkouts.Include(w => w.Segments)
            .FirstOrDefaultAsync(w => w.Id == workoutId, cancellationToken)
            ?? throw new NotFoundException("PlannedWorkout", workoutId);

        var athleteUserId = await ResolveAthleteForWorkoutAsync(workout, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewTrainingPlan, cancellationToken);

        return ToWorkoutDto(workout);
    }

    public async Task<PlannedWorkoutDto> CreateWorkoutAsync(CreatePlannedWorkoutRequest request, CancellationToken cancellationToken = default)
    {
        var week = await db.TrainingWeeks.FirstOrDefaultAsync(w => w.Id == request.TrainingWeekId, cancellationToken)
            ?? throw new NotFoundException("TrainingWeek", request.TrainingWeekId);
        var athleteUserId = await db.TrainingPlans.Where(p => p.Id == week.TrainingPlanId).Select(p => p.AthleteUserId).FirstAsync(cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var workout = new PlannedWorkout
        {
            TrainingWeekId = request.TrainingWeekId,
            Date = request.Date,
            Sport = request.Sport,
            Title = request.Title,
            CoachDescription = request.CoachDescription,
            IsRestDay = request.IsRestDay,
            PlannedDistanceMeters = request.PlannedDistanceMeters,
            PlannedDurationSeconds = request.PlannedDurationSeconds,
            PlannedElevationGainMeters = request.PlannedElevationGainMeters,
            CreatedAtUtc = clock.UtcNow,
            Segments = MapSegments(request.Segments),
        };
        db.PlannedWorkouts.Add(workout);
        await db.SaveChangesAsync(cancellationToken);
        return ToWorkoutDto(workout);
    }

    public async Task<PlannedWorkoutDto> UpdateWorkoutAsync(Guid workoutId, UpdatePlannedWorkoutRequest request, CancellationToken cancellationToken = default)
    {
        var workout = await db.PlannedWorkouts.Include(w => w.Segments)
            .FirstOrDefaultAsync(w => w.Id == workoutId, cancellationToken)
            ?? throw new NotFoundException("PlannedWorkout", workoutId);

        var athleteUserId = await ResolveAthleteForWorkoutAsync(workout, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        workout.Date = request.Date;
        workout.Sport = request.Sport;
        workout.Title = request.Title;
        workout.CoachDescription = request.CoachDescription;
        workout.IsRestDay = request.IsRestDay;
        workout.PlannedDistanceMeters = request.PlannedDistanceMeters;
        workout.PlannedDurationSeconds = request.PlannedDurationSeconds;
        workout.PlannedElevationGainMeters = request.PlannedElevationGainMeters;
        workout.UpdatedAtUtc = clock.UtcNow;

        db.WorkoutSegments.RemoveRange(workout.Segments);
        var newSegments = MapSegments(request.Segments);
        workout.Segments = newSegments;
        // WorkoutSegment.Id is assigned client-side at construction (see Entity base type), so
        // EF's change tracker can't tell these are new from the key alone — without an explicit
        // Add, it infers Unchanged/Modified from graph fixup and issues an UPDATE against a row
        // that was never inserted, throwing DbUpdateConcurrencyException (0 rows affected).
        db.WorkoutSegments.AddRange(newSegments);

        await db.SaveChangesAsync(cancellationToken);
        return ToWorkoutDto(workout);
    }

    public async Task DeleteWorkoutAsync(Guid workoutId, CancellationToken cancellationToken = default)
    {
        var workout = await db.PlannedWorkouts.FirstOrDefaultAsync(w => w.Id == workoutId, cancellationToken)
            ?? throw new NotFoundException("PlannedWorkout", workoutId);

        var athleteUserId = await ResolveAthleteForWorkoutAsync(workout, cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        workout.IsDeleted = true;
        workout.DeletedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlannedWorkoutDto> CopyWorkoutAsync(Guid workoutId, CopyWorkoutRequest request, CancellationToken cancellationToken = default)
    {
        var source = await db.PlannedWorkouts.Include(w => w.Segments)
            .FirstOrDefaultAsync(w => w.Id == workoutId, cancellationToken)
            ?? throw new NotFoundException("PlannedWorkout", workoutId);

        var targetWeek = await db.TrainingWeeks.FirstOrDefaultAsync(w => w.Id == request.TargetTrainingWeekId, cancellationToken)
            ?? throw new NotFoundException("TrainingWeek", request.TargetTrainingWeekId);
        var athleteUserId = await db.TrainingPlans.Where(p => p.Id == targetWeek.TrainingPlanId).Select(p => p.AthleteUserId).FirstAsync(cancellationToken);
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.EditTrainingPlan, cancellationToken);

        var copy = new PlannedWorkout
        {
            TrainingWeekId = request.TargetTrainingWeekId,
            Date = request.TargetDate,
            Sport = source.Sport,
            Title = source.Title,
            CoachDescription = source.CoachDescription,
            IsRestDay = source.IsRestDay,
            PlannedDistanceMeters = source.PlannedDistanceMeters,
            PlannedDurationSeconds = source.PlannedDurationSeconds,
            PlannedElevationGainMeters = source.PlannedElevationGainMeters,
            CopiedFromWorkoutId = source.Id,
            CreatedAtUtc = clock.UtcNow,
            Segments = source.Segments.Select(s => new WorkoutSegment
            {
                Order = s.Order,
                Type = s.Type,
                RepeatCount = s.RepeatCount,
                DistanceMeters = s.DistanceMeters,
                DurationSeconds = s.DurationSeconds,
                IntensityTargetType = s.IntensityTargetType,
                TargetHeartRateZoneId = s.TargetHeartRateZoneId,
                TargetPaceSecondsPerKmMin = s.TargetPaceSecondsPerKmMin,
                TargetPaceSecondsPerKmMax = s.TargetPaceSecondsPerKmMax,
                TargetRpe = s.TargetRpe,
                TargetPowerWatts = s.TargetPowerWatts,
                Notes = s.Notes,
            }).ToList(),
        };

        db.PlannedWorkouts.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return ToWorkoutDto(copy);
    }

    private async Task<Guid> ResolveAthleteForWorkoutAsync(PlannedWorkout workout, CancellationToken cancellationToken)
    {
        return await db.TrainingWeeks
            .Where(w => w.Id == workout.TrainingWeekId)
            .Join(db.TrainingPlans, w => w.TrainingPlanId, p => p.Id, (w, p) => p.AthleteUserId)
            .FirstAsync(cancellationToken);
    }

    private static List<WorkoutSegment> MapSegments(IReadOnlyList<WorkoutSegmentDto>? segments)
    {
        if (segments is null)
        {
            return [];
        }

        return segments.Select(s => new WorkoutSegment
        {
            Order = s.Order,
            Type = s.Type,
            RepeatCount = s.RepeatCount,
            DistanceMeters = s.DistanceMeters,
            DurationSeconds = s.DurationSeconds,
            IntensityTargetType = s.IntensityTargetType,
            TargetHeartRateZoneId = s.TargetHeartRateZoneId,
            TargetPaceSecondsPerKmMin = s.TargetPaceSecondsPerKmMin,
            TargetPaceSecondsPerKmMax = s.TargetPaceSecondsPerKmMax,
            TargetRpe = s.TargetRpe,
            TargetPowerWatts = s.TargetPowerWatts,
            Notes = s.Notes,
        }).ToList();
    }

    private static TrainingPlanDto ToDto(TrainingPlan plan) => new(
        plan.Id, plan.AthleteUserId, plan.CoachUserId, plan.SeasonId, plan.Name, plan.StartDate, plan.EndDate, plan.IsActive);

    private static TrainingWeekDto ToWeekDto(TrainingWeek week) => new(
        week.Id, week.TrainingPlanId, week.WeekStartDate, week.WeekIndex,
        week.CoachWeekSummary, week.AthleteWeekReflection, week.WhatWasMissingNote, week.AthleteWeeklyRating,
        week.Workouts.Where(w => !w.IsDeleted).OrderBy(w => w.Date).Select(ToWorkoutDto).ToList());

    private static PlannedWorkoutDto ToWorkoutDto(PlannedWorkout workout) => new(
        workout.Id, workout.TrainingWeekId, workout.Date, workout.Sport, workout.Title, workout.CoachDescription, workout.IsRestDay,
        workout.PlannedDistanceMeters, workout.PlannedDurationSeconds, workout.PlannedElevationGainMeters,
        workout.Segments.OrderBy(s => s.Order).Select(s => new WorkoutSegmentDto(
            s.Id, s.Order, s.Type, s.RepeatCount, s.DistanceMeters, s.DurationSeconds, s.IntensityTargetType,
            s.TargetHeartRateZoneId, s.TargetPaceSecondsPerKmMin, s.TargetPaceSecondsPerKmMax, s.TargetRpe, s.TargetPowerWatts, s.Notes)).ToList());
}
