using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Planning;
using TrainCoach.Integrations.IntervalsIcu;

namespace TrainCoach.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A test-double provider standing in for a real adapter (Strava, intervals.icu, ...) so sync
/// pipeline tests can control exactly what "arrives from the source" without any network call.
/// Returns whatever activities/wellness samples are in the mutable lists at the time
/// FetchRecentActivitiesAsync/FetchWellnessAsync are called. <paramref name="files"/> (external
/// id → original activity file) makes it an <see cref="IActivityFileProvider"/> like intervals.icu.
/// </summary>
public class FakeIntegrationProvider(
    IntegrationProviderType providerType, List<ExternalActivity> activities, List<ExternalWellnessSample>? wellness = null,
    Dictionary<string, byte[]>? files = null)
    : IIntegrationProvider, IWellnessDataProvider, IActivityFileProvider, ITrainingSettingsSyncProvider, IPlannedWorkoutPushProvider
{
    /// <summary>Every workout pushed (created or updated), in call order.</summary>
    public List<(Guid WorkoutId, string Title, DateOnly Date, string Description)> PushedWorkouts { get; } = [];

    /// <summary>Every workout removed from the calendar, in call order.</summary>
    public List<Guid> RemovedWorkouts { get; } = [];

    public string WorkoutPushScope => "CALENDAR:WRITE";

    /// <summary>Same rules as the real adapter: the intervals.icu text builder decides what's pushable.</summary>
    public Task<PlannedWorkoutPushResult?> UpsertWorkoutAsync(string accessToken, PlannedWorkout workout, CancellationToken cancellationToken = default)
    {
        if (IntervalsIcuWorkoutDescriptionBuilder.Build(workout) is not { } ev) return Task.FromResult<PlannedWorkoutPushResult?>(null);
        lock (PushedWorkouts)
        {
            PushedWorkouts.Add((workout.Id, workout.Title, workout.Date, ev.Description));
        }
        return Task.FromResult<PlannedWorkoutPushResult?>(new PlannedWorkoutPushResult("4242", ev.Warnings.Select(w => w.ToString()).ToList()));
    }

    public Task RemoveWorkoutAsync(string accessToken, Guid plannedWorkoutId, CancellationToken cancellationToken = default)
    {
        lock (RemovedWorkouts)
        {
            RemovedWorkouts.Add(plannedWorkoutId);
        }
        return Task.CompletedTask;
    }

    /// <summary>Every settings write via <see cref="PushTrainingSettingsAsync"/>, in call order.</summary>
    public List<TrainingSettingsUpdate> PushedSettings { get; } = [];

    /// <summary>When set, <see cref="PushTrainingSettingsAsync"/> rejects with this message.</summary>
    public string? SettingsPushError { get; set; }

    public string SettingsSyncScope => "SETTINGS:WRITE";

    public Task PushTrainingSettingsAsync(string accessToken, TrainingSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        if (SettingsPushError is not null) throw new TrainingSettingsSyncException(SettingsPushError);
        lock (PushedSettings)
        {
            PushedSettings.Add(update);
        }
        return Task.CompletedTask;
    }

    /// <summary>Every <c>sinceUtc</c> the sync asked for, in call order.</summary>
    public List<DateTime> RequestedSince { get; } = [];

    /// <summary>External ids whose file was downloaded, in call order.</summary>
    public List<string> DownloadedFiles { get; } = [];

    public IntegrationProviderType ProviderType => providerType;
    public bool RequiresOAuthRedirect => false;

    public string BuildAuthorizationUrl(string state) => "https://fake.test/authorize";

    public Task<ExternalTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalTokenResult("fake-access-token", null, null, "fake-account", null));

    public Task<ExternalTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalTokenResult("fake-access-token-refreshed", null, null, "fake-account", null));

    public Task<IReadOnlyList<ExternalActivity>> FetchRecentActivitiesAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        lock (RequestedSince)
        {
            RequestedSince.Add(sinceUtc);
        }
        return Task.FromResult<IReadOnlyList<ExternalActivity>>(activities.ToList());
    }

    public Task<byte[]?> DownloadActivityFileAsync(string accessToken, string externalActivityId, CancellationToken cancellationToken = default)
    {
        lock (DownloadedFiles)
        {
            DownloadedFiles.Add(externalActivityId);
        }
        return Task.FromResult(files is not null && files.TryGetValue(externalActivityId, out var file) ? file : null);
    }

    public Task<IReadOnlyList<ExternalWellnessSample>> FetchWellnessAsync(string accessToken, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalWellnessSample>>((wellness ?? []).ToList());
}
