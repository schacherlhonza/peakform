using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// The app-wide "sync everything" flow behind the header sync indicator: queue → Pending run →
/// picked up by the real background worker → Succeeded, observed only through the HTTP API.
/// </summary>
public class SyncAllApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private FakeProviderApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new FakeProviderApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private record AuthResultDto(string UserId, string AccessToken);
    private record RunDto(string Status, string Trigger, int ItemsCreated);
    private record StatusDto(string Provider, RunDto? LatestRun);

    private async Task<Guid> RegisterAndAuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@test.cz",
            password = "Heslo123",
            firstName = "Test",
            lastName = "Athlete",
            role = "Athlete",
            timeZoneId = "Europe/Prague",
            locale = "cs-CZ",
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return Guid.Parse(auth.UserId);
    }

    private async Task ConnectAsync(Guid athleteUserId, IntegrationProviderType provider, DateTime? lastSyncedAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var tokenEncryptor = scope.ServiceProvider.GetRequiredService<ITokenEncryptor>();
        db.IntegrationConnections.Add(new IntegrationConnection
        {
            AthleteUserId = athleteUserId,
            Provider = provider,
            Status = IntegrationConnectionStatus.Connected,
            ConnectedAtUtc = DateTime.UtcNow,
            LastSyncedAtUtc = lastSyncedAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            Credential = new IntegrationCredential { EncryptedAccessToken = tokenEncryptor.Protect("fake-access-token") },
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<StatusDto>> GetStatusAsync() =>
        (await _client.GetFromJsonAsync<List<StatusDto>>("/api/integrations/sync-status", JsonOptions))!;

    [Fact]
    public async Task SyncAll_QueuesEveryConnection_AndStatusReportsSucceeded()
    {
        var athleteUserId = await RegisterAndAuthenticateAsync();
        await ConnectAsync(athleteUserId, IntegrationProviderType.Strava);
        await ConnectAsync(athleteUserId, IntegrationProviderType.IntervalsIcu);
        _factory.StravaActivities.Add(new ExternalActivity(
            "strava-1", SportType.Running, "Ranní běh", DateTime.UtcNow.AddHours(-3), 3600, 10000,
            null, null, null, null, null, null));

        var response = await _client.PostAsync("/api/integrations/sync-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var queued = (await response.Content.ReadFromJsonAsync<List<StatusDto>>(JsonOptions))!;
        queued.Should().HaveCount(2);
        queued.Should().OnlyContain(s => s.LatestRun != null && s.LatestRun.Trigger == "Manual");

        List<StatusDto> status = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            status = await GetStatusAsync();
            if (status.All(s => s.LatestRun?.Status is "Succeeded" or "Failed")) break;
            await Task.Delay(100);
        }

        status.Should().OnlyContain(s => s.LatestRun!.Status == "Succeeded");
        status.Single(s => s.Provider == "Strava").LatestRun!.ItemsCreated.Should().Be(1);

        // The worker reused the Pending row rather than adding a second run per sync.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        (await db.SynchronizationRuns.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task AutomaticSyncAll_SkipsRecentlySyncedConnections()
    {
        var athleteUserId = await RegisterAndAuthenticateAsync();
        await ConnectAsync(athleteUserId, IntegrationProviderType.Strava, lastSyncedAtUtc: DateTime.UtcNow.AddMinutes(-2));
        await ConnectAsync(athleteUserId, IntegrationProviderType.IntervalsIcu, lastSyncedAtUtc: DateTime.UtcNow.AddDays(-1));

        var response = await _client.PostAsync("/api/integrations/sync-all?automatic=true", null);
        var queued = (await response.Content.ReadFromJsonAsync<List<StatusDto>>(JsonOptions))!;

        queued.Single(s => s.Provider == "Strava").LatestRun.Should().BeNull();
        queued.Single(s => s.Provider == "IntervalsIcu").LatestRun!.Trigger.Should().Be("OnLogin");
    }
}
