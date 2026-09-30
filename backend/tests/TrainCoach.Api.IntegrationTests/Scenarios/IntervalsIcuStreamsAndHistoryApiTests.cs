using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dynastream.Fit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;
using TrainCoach.Infrastructure.Persistence;
using Xunit;
using DateTime = System.DateTime;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// intervals.icu detail streams: after a sync, the original device file of each activity is
/// downloaded once, parsed and stored (GPS included); plus the athlete-requested history backfill.
/// </summary>
public class IntervalsIcuStreamsAndHistoryApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTime RunStart = DateTime.UtcNow.Date.AddDays(-2).AddHours(7);
    private FakeProviderApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _athleteUserId;

    private record AuthResultDto(string UserId, string AccessToken);
    private record StreamsDto(List<int> TimeOffsetsSeconds, List<double?>? Latitude, List<int?>? HeartRateBpm, string Source);
    private record RunDto(string Status, string Trigger);

    public async Task InitializeAsync()
    {
        _factory = new FakeProviderApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{Guid.NewGuid():N}@test.cz", password = "Heslo123", firstName = "Test", lastName = "Athlete",
            role = "Athlete", timeZoneId = "Europe/Prague", locale = "cs-CZ",
        });
        var auth = (await response.Content.ReadFromJsonAsync<AuthResultDto>(JsonOptions))!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        _athleteUserId = Guid.Parse(auth.UserId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var tokenEncryptor = scope.ServiceProvider.GetRequiredService<ITokenEncryptor>();
        db.IntegrationConnections.Add(new IntegrationConnection
        {
            AthleteUserId = _athleteUserId,
            Provider = IntegrationProviderType.IntervalsIcu,
            Status = IntegrationConnectionStatus.Connected,
            ConnectedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Credential = new IntegrationCredential { EncryptedAccessToken = tokenEncryptor.Protect("fake-access-token") },
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>10-minute outdoor run as FIT with GPS, gzipped like intervals.icu may serve it.</summary>
    private static byte[] GzippedFitRun(DateTime start)
    {
        using var fit = new MemoryStream();
        var encoder = new Encode(ProtocolVersion.V20);
        encoder.Open(fit);
        var fileId = new FileIdMesg();
        fileId.SetType(Dynastream.Fit.File.Activity);
        fileId.SetManufacturer(Manufacturer.Garmin);
        fileId.SetSerialNumber(3602364024);
        fileId.SetTimeCreated(new Dynastream.Fit.DateTime(start));
        encoder.Write(fileId);
        for (var s = 0; s < 600; s++)
        {
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(start.AddSeconds(s)));
            record.SetHeartRate((byte)(140 + s % 10));
            record.SetPositionLat((int)((50.0 + s * 3.0 / 111_195) / (180.0 / 2147483648.0)));
            record.SetPositionLong((int)(14.4 / (180.0 / 2147483648.0)));
            record.SetDistance(s * 3f);
            encoder.Write(record);
        }
        encoder.Close();

        using var gz = new MemoryStream();
        using (var gzip = new GZipStream(gz, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(fit.ToArray());
        }
        return gz.ToArray();
    }

    private async Task WaitUntilAsync(Func<TrainCoachDbContext, Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                if (await condition(scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>()))
                {
                    return;
                }
            }
            await Task.Delay(100);
        }
        throw new TimeoutException();
    }

    private async Task SyncAndWaitForBackfillAsync()
    {
        (await _client.PostAsync("/api/integrations/IntervalsIcu/sync", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        // Backfill is queued after the sync — done once every intervals.icu record was attempted.
        await WaitUntilAsync(async db =>
            await db.ActivitySourceRecords.AnyAsync(sr => sr.Source == DataSource.IntervalsIcu)
            && !await db.ActivitySourceRecords.AnyAsync(sr => sr.Source == DataSource.IntervalsIcu && sr.StreamFetchAttemptedAtUtc == null));
    }

    [Fact]
    public async Task Sync_downloads_each_activity_file_once_and_serves_the_stored_stream_with_gps()
    {
        _factory.IntervalsIcuActivities.Add(new ExternalActivity("i1", SportType.Running, "Ranní běh", RunStart, 600, 1800, null, 145, 150, null, null, null, DeviceName: "Garmin Forerunner 965"));
        _factory.IntervalsIcuActivities.Add(new ExternalActivity("i2", SportType.Strength, "Posilování", RunStart.AddHours(8), 1800, null, null, null, null, null, null, null));
        _factory.IntervalsIcuFiles["i1"] = GzippedFitRun(RunStart);

        await SyncAndWaitForBackfillAsync();

        Guid runId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var record = await db.ActivitySourceRecords.Include(sr => sr.Stream).SingleAsync(sr => sr.ExternalId == "i1");
            record.Stream.Should().NotBeNull();
            record.Stream!.Origin.Should().Be(ActivityStreamOrigin.IntervalsIcuFile);
            record.FitFileUuid.Should().StartWith("fit:1:3602364024:", "the FIT identity is picked up for level-3 matching");
            (await db.ActivityStreams.CountAsync()).Should().Be(1, "the strength session has no file");
            runId = record.CompletedActivityId;
        }

        var streams = (await _client.GetFromJsonAsync<StreamsDto>($"/api/activities/{runId}/streams", JsonOptions))!;
        streams.Source.Should().Be("Stored");
        streams.Latitude![0].Should().BeApproximately(50.0, 1e-5);
        streams.HeartRateBpm![3].Should().Be(143);

        var activity = await _client.GetFromJsonAsync<JsonElement>($"/api/activities/{runId}", JsonOptions);
        activity.GetProperty("deviceName").GetString().Should().Be("Garmin Forerunner 965");

        // A second sync must not download anything again — including the file-less activity.
        var downloadsBefore = _factory.IntervalsIcuProvider.DownloadedFiles.Count;
        (await _client.PostAsync("/api/integrations/IntervalsIcu/sync", null)).EnsureSuccessStatusCode();
        await WaitUntilAsync(async db => await db.SynchronizationRuns.CountAsync(r => r.Status == SyncRunStatus.Succeeded) >= 2);
        await Task.Delay(500);
        _factory.IntervalsIcuProvider.DownloadedFiles.Should().HaveCount(downloadsBefore);
        downloadsBefore.Should().Be(2);
    }

    [Fact]
    public async Task History_backfill_syncs_from_the_requested_date_without_moving_the_sync_cursor_back()
    {
        await SyncAndWaitForBackfillIfAnyAsync();
        DateTime? cursorBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            cursorBefore = (await scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>().IntegrationConnections.SingleAsync()).LastSyncedAtUtc;
        }

        var response = await _client.PostAsJsonAsync("/api/integrations/IntervalsIcu/history", new { fromDate = "2023-01-01" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await WaitUntilAsync(async db => await db.SynchronizationRuns.AnyAsync(r => r.Trigger == SyncTrigger.HistoryBackfill && r.Status == SyncRunStatus.Succeeded));
        _factory.IntervalsIcuProvider.RequestedSince.Should().Contain(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        using (var scope = _factory.Services.CreateScope())
        {
            var connection = await scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>().IntegrationConnections.SingleAsync();
            connection.LastSyncedAtUtc.Should().Be(cursorBefore);
        }

        var runs = (await _client.GetFromJsonAsync<List<RunDto>>("/api/integrations/IntervalsIcu/sync-history", JsonOptions))!;
        runs.Should().Contain(r => r.Trigger == "HistoryBackfill");
    }

    private async Task SyncAndWaitForBackfillIfAnyAsync()
    {
        (await _client.PostAsync("/api/integrations/IntervalsIcu/sync", null)).EnsureSuccessStatusCode();
        await WaitUntilAsync(async db => await db.SynchronizationRuns.AnyAsync(r => r.Status == SyncRunStatus.Succeeded));
    }

    [Theory]
    [InlineData("Strava", "2023-01-01")]
    [InlineData("IntervalsIcu", "2999-01-01")]
    public async Task History_backfill_is_refused_for_strava_and_future_dates(string provider, string fromDate)
    {
        var response = await _client.PostAsJsonAsync($"/api/integrations/{provider}/history", new { fromDate });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
