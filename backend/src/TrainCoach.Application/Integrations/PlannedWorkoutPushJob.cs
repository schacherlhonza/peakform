using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;
using TrainCoach.Domain.Planning;

namespace TrainCoach.Application.Integrations;

public interface IPlannedWorkoutPushJob
{
    /// <summary>Brings one workout's entry on every push-enabled calendar in line with the workout:
    /// pushed while it is a current, non-rest workout and the athlete allows pushing, removed otherwise.</summary>
    Task RunAsync(Guid plannedWorkoutId, CancellationToken cancellationToken = default);

    /// <summary>The same for every upcoming workout of the athlete and every one still on a calendar —
    /// after pushing is switched on/off or the account is reconnected.</summary>
    Task RunForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Queued after every change of a planned workout (TrainingPlanService) and after the athlete toggles
/// pushing. Best effort: failures never affect the plan in PeakForm, they're stored on the
/// <see cref="PlannedWorkoutPushRecord"/> and shown to the coach. Past workouts aren't pushed — they're
/// no use on a watch — but a past entry is still removed when its workout goes away.
/// </summary>
public class PlannedWorkoutPushJob(
    IApplicationDbContext db,
    IEnumerable<IIntegrationProvider> providers,
    IAccessTokenResolver tokenResolver,
    IDateTimeProvider clock,
    ILogger<PlannedWorkoutPushJob> logger) : IPlannedWorkoutPushJob
{
    public async Task RunForAthleteAsync(Guid athleteUserId, CancellationToken cancellationToken = default)
    {
        var from = FirstPushableDate();
        var ids = await db.PlannedWorkouts.IgnoreQueryFilters()
            .Where(w => w.TrainingWeek.TrainingPlan.AthleteUserId == athleteUserId
                && ((w.Date >= from && !w.IsDeleted) || w.PushRecords.Any(r => r.Status != WorkoutPushStatus.Removed)))
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await RunAsync(id, cancellationToken);
        }
    }

    public async Task RunAsync(Guid plannedWorkoutId, CancellationToken cancellationToken = default)
    {
        // Deleted plans/workouts are loaded on purpose: their calendar entries have to go.
        var workout = await db.PlannedWorkouts.IgnoreQueryFilters()
            .Include(w => w.Segments)
            .Include(w => w.PushRecords)
            .Include(w => w.TrainingWeek).ThenInclude(w => w.TrainingPlan)
            .FirstOrDefaultAsync(w => w.Id == plannedWorkoutId, cancellationToken);
        if (workout is null)
        {
            return;
        }

        var pushProviders = providers.Where(p => p is IPlannedWorkoutPushProvider).ToDictionary(p => p.ProviderType);
        var connections = await db.IntegrationConnections
            .Include(c => c.Credential)
            .Where(c => c.AthleteUserId == workout.TrainingWeek.TrainingPlan.AthleteUserId && c.Status == IntegrationConnectionStatus.Connected)
            .ToListAsync(cancellationToken);

        var belongsOnCalendar = !workout.IsDeleted && !workout.TrainingWeek.TrainingPlan.IsDeleted && !workout.IsRestDay;
        foreach (var connection in connections.Where(c => pushProviders.ContainsKey(c.Provider)))
        {
            var provider = (IPlannedWorkoutPushProvider)pushProviders[connection.Provider];
            var record = workout.PushRecords.FirstOrDefault(r => r.Provider == connection.Provider);
            var onCalendar = record is not null && record.Status != WorkoutPushStatus.Removed;

            if (belongsOnCalendar && connection.PushPlannedWorkouts)
            {
                if (workout.Date >= FirstPushableDate())
                {
                    record ??= NewRecord(workout, connection.Provider);
                    await PushAsync(workout, connection, provider, record, cancellationToken);
                }
            }
            else if (onCalendar || record?.Status == WorkoutPushStatus.Failed)
            {
                await RemoveAsync(workout, connection, provider, record!, cancellationToken);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A day of slack: "today" in the athlete's time zone may still be yesterday in UTC.</summary>
    private DateOnly FirstPushableDate() => DateOnly.FromDateTime(clock.UtcNow).AddDays(-1);

    private PlannedWorkoutPushRecord NewRecord(PlannedWorkout workout, IntegrationProviderType provider)
    {
        var record = new PlannedWorkoutPushRecord { PlannedWorkoutId = workout.Id, Provider = provider, UpdatedAtUtc = clock.UtcNow };
        workout.PushRecords.Add(record);
        db.PlannedWorkoutPushRecords.Add(record);
        return record;
    }

    private async Task PushAsync(PlannedWorkout workout, IntegrationConnection connection, IPlannedWorkoutPushProvider provider, PlannedWorkoutPushRecord record, CancellationToken cancellationToken)
    {
        record.UpdatedAtUtc = clock.UtcNow;
        var token = await TokenOrErrorAsync(connection, provider, record, cancellationToken);
        if (token is null)
        {
            return;
        }

        try
        {
            var result = await provider.UpsertWorkoutAsync(token, workout, cancellationToken);
            if (result is null)
            {
                await RemoveAsync(workout, connection, provider, record, cancellationToken);
                return;
            }
            record.Status = WorkoutPushStatus.Pushed;
            record.ExternalEventId = result.ExternalEventId;
            record.Warnings = result.Warnings.Count > 0 ? string.Join(',', result.Warnings) : null;
            record.PushedAtUtc = clock.UtcNow;
            record.Error = null;
        }
        catch (Exception ex) when (ex is WorkoutPushException or HttpRequestException)
        {
            Fail(record, ex, workout.Id, connection.Provider);
        }
    }

    private async Task RemoveAsync(PlannedWorkout workout, IntegrationConnection connection, IPlannedWorkoutPushProvider provider, PlannedWorkoutPushRecord record, CancellationToken cancellationToken)
    {
        record.UpdatedAtUtc = clock.UtcNow;
        var token = await TokenOrErrorAsync(connection, provider, record, cancellationToken);
        if (token is null)
        {
            return;
        }

        try
        {
            await provider.RemoveWorkoutAsync(token, workout.Id, cancellationToken);
            record.Status = WorkoutPushStatus.Removed;
            record.Error = null;
            record.Warnings = null;
        }
        catch (Exception ex) when (ex is WorkoutPushException or HttpRequestException)
        {
            Fail(record, ex, workout.Id, connection.Provider);
        }
    }

    private async Task<string?> TokenOrErrorAsync(IntegrationConnection connection, IPlannedWorkoutPushProvider provider, PlannedWorkoutPushRecord record, CancellationToken cancellationToken)
    {
        var scopes = (connection.Credential?.GrantedScope ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        if (!scopes.Contains(provider.WorkoutPushScope, StringComparer.OrdinalIgnoreCase))
        {
            record.Status = WorkoutPushStatus.Failed;
            record.Error = "Chybí oprávnění k zápisu do kalendáře — athlete musí znovu připojit účet.";
            return null;
        }

        var token = await tokenResolver.ResolveFreshAccessTokenAsync(connection.AthleteUserId, connection.Provider, cancellationToken);
        if (token is null)
        {
            record.Status = WorkoutPushStatus.Failed;
            record.Error = "Připojení vypršelo — athlete musí znovu připojit účet.";
        }
        return token;
    }

    private void Fail(PlannedWorkoutPushRecord record, Exception ex, Guid workoutId, IntegrationProviderType provider)
    {
        if (ex is HttpRequestException)
        {
            logger.LogWarning(ex, "Odeslání tréninku {WorkoutId} do {Provider} selhalo.", workoutId, provider);
        }
        record.Status = WorkoutPushStatus.Failed;
        record.Error = ex is WorkoutPushException ? ex.Message : "Poskytovatel není dostupný — zkusí se znovu při další změně tréninku.";
    }
}
