using System.IO.Compression;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Application.Execution.Streams;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Infrastructure.Persistence;
using TrainCoach.Integrations.StravaArchive;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>GDPR export of stored activity streams: GPX for routes, CSV otherwise.</summary>
public class StreamExportApiTests : IntegrationTestBase
{
    private static readonly DateTime Start = new(2024, 5, 1, 6, 0, 0, DateTimeKind.Utc);

    private async Task SeedAsync(Guid athleteUserId, string externalId, SportType sport, bool withGps, DateTime start)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
        var builder = new ActivityStreamBuilder();
        for (var s = 0; s < 300; s++)
        {
            builder.Add(new ActivityStreamBuilder.Sample(start.AddSeconds(s), HeartRate: 140 + s % 5, Power: withGps ? null : 200,
                Latitude: withGps ? 50.0 + s * 3.0 / 111_195 : null, Longitude: withGps ? 14.4 : null, Altitude: 200, Distance: s * 3.0));
        }
        var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = externalId, FetchedAtUtc = DateTime.UtcNow };
        db.CompletedActivities.Add(new CompletedActivity
        {
            AthleteUserId = athleteUserId, Sport = sport, Title = withGps ? "Ranní běh" : "Trenažér", StartedAtUtc = start, DurationSeconds = 300,
            CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
        });
        db.ActivityStreams.Add(ActivityStreamMapping.ToEntity(record.Id, builder.Build()!, ActivityStreamOrigin.StravaArchive, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Export_contains_a_gpx_per_route_and_a_csv_per_stream_without_gps_and_only_own_data()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var other = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        await SeedAsync(Guid.Parse(auth.UserId), $"{auth.UserId}-run", SportType.Running, withGps: true, Start);
        await SeedAsync(Guid.Parse(auth.UserId), $"{auth.UserId}-ride", SportType.Cycling, withGps: false, Start.AddDays(1));
        await SeedAsync(Guid.Parse(other.UserId), $"{other.UserId}-run", SportType.Running, withGps: true, Start);

        var response = await AuthenticatedClient(auth).GetAsync("/api/account/export/streams");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync());
        zip.Entries.Select(e => Path.GetExtension(e.Name)).Should().BeEquivalentTo([".gpx", ".csv"], "the other athlete's activity isn't included");

        // The GPX reads back with the same parser that reads Strava's archive.
        await using var gpx = zip.Entries.Single(e => e.Name.EndsWith(".gpx")).Open();
        using var buffer = new MemoryStream();
        await gpx.CopyToAsync(buffer);
        buffer.Position = 0;
        var parsed = new ActivityFileProbe().ReadStream(buffer, ActivityFileFormat.Gpx)!;
        parsed.Count.Should().Be(300);
        parsed.HeartRateBpm![2].Should().Be(142);
        parsed.Latitude![0].Should().BeApproximately(50.0, 1e-6);

        using var csv = new StreamReader(zip.Entries.Single(e => e.Name.EndsWith(".csv")).Open());
        (await csv.ReadLineAsync()).Should().StartWith("time_utc,offset_s,heart_rate_bpm,power_w");
        (await csv.ReadLineAsync()).Should().Be("2024-05-02T06:00:00Z,0,140,200,,0.0,200.0,,");
    }
}
