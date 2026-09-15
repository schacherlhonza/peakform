using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Wellness;

namespace TrainCoach.Application.Wellness;

public class CheckInService(
    IApplicationDbContext db,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock,
    IBackgroundJobQueue jobQueue) : ICheckInService
{
    public async Task<DailyCheckInDto> SubmitAsync(Guid callerUserId, SubmitCheckInRequest request, CancellationToken cancellationToken = default)
    {
        if (callerUserId != request.AthleteUserId)
        {
            throw new ForbiddenAccessException("Check-in lze vyplnit pouze za sebe.");
        }

        var existing = await db.DailyCheckIns.FirstOrDefaultAsync(
            c => c.AthleteUserId == request.AthleteUserId && c.Date == request.Date && c.Type == request.Type,
            cancellationToken);

        if (existing is null)
        {
            existing = new DailyCheckIn
            {
                AthleteUserId = request.AthleteUserId,
                Date = request.Date,
                Type = request.Type,
                CreatedAtUtc = clock.UtcNow,
            };
            db.DailyCheckIns.Add(existing);
        }

        existing.Energy = request.Energy;
        existing.Fatigue = request.Fatigue;
        existing.LegsFeeling = request.LegsFeeling;
        existing.Stress = request.Stress;
        existing.HasPainOrIllness = request.HasPainOrIllness;
        existing.Note = request.Note;
        existing.SleepQuality = request.SleepQuality;
        existing.MuscleSoreness = request.MuscleSoreness;
        existing.Motivation = request.Motivation;
        existing.HydrationLiters = request.HydrationLiters;
        existing.MealQuality = request.MealQuality;
        existing.CompletedPlannedWorkout = request.CompletedPlannedWorkout;
        existing.Rpe = request.Rpe;
        existing.UpdatedAtUtc = clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        // The evening check-in is the trigger for the day's automatic report — the athlete never
        // has to remember to ask for it. Morning reports are generated on first read instead
        // (see ReportsController), since there is no natural "submit" event before the day starts.
        if (request.Type == CheckInType.Evening)
        {
            await jobQueue.QueueReportGenerationAsync(request.AthleteUserId, ReportType.Evening, request.Date, cancellationToken);
        }

        return ToDto(existing);
    }

    public async Task<DailyCheckInDto?> GetAsync(Guid athleteUserId, DateOnly date, CheckInType type, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var checkIn = await db.DailyCheckIns.FirstOrDefaultAsync(
            c => c.AthleteUserId == athleteUserId && c.Date == date && c.Type == type, cancellationToken);
        return checkIn is null ? null : ToDto(checkIn);
    }

    public async Task<IReadOnlyList<DailyCheckInDto>> GetRangeAsync(Guid athleteUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var checkIns = await db.DailyCheckIns
            .Where(c => c.AthleteUserId == athleteUserId && c.Date >= from && c.Date <= to)
            .OrderByDescending(c => c.Date)
            .ToListAsync(cancellationToken);

        return checkIns.Select(ToDto).ToList();
    }

    private static DailyCheckInDto ToDto(DailyCheckIn c) => new(
        c.Id, c.AthleteUserId, c.Date, c.Type, c.Energy, c.Fatigue, c.LegsFeeling, c.Stress, c.HasPainOrIllness, c.Note,
        c.SleepQuality, c.MuscleSoreness, c.Motivation, c.HydrationLiters, c.MealQuality, c.CompletedPlannedWorkout, c.Rpe);
}
