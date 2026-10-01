using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Dynastream.Fit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Infrastructure.Persistence;
using Xunit;
using DateTime = System.DateTime;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// Phase 2 of the Strava archive import: activity files become stored, downsampled detail streams
/// (charts + GPS route) served by /api/activities/{id}/streams without any provider call —
/// including backfilling streams onto activities that already existed. Synthetic files only.
/// </summary>
public class StravaArchiveStreamsApiTests : IntegrationTestBase
{
    private const string Route = "/api/integrations/strava/archive-imports";
    private static readonly DateTime RunStart = new(2024, 3, 3, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RideStart = new(2024, 3, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OldRunStart = new(2024, 2, 1, 6, 30, 0, DateTimeKind.Utc);

    private record ImportDto(Guid Id, string Status, int? PreviewStreamsToAdd, int ItemsCreated, int ItemsSkippedDuplicate, int ItemsStreamsAdded, string? ErrorMessage);
    private record StreamsDto(
        List<int> TimeOffsetsSeconds, List<int?>? HeartRateBpm, List<int?>? CadenceRpm, List<int?>? PowerWatts,
        List<decimal?>? DistanceMeters, List<decimal?>? ElevationMeters, List<int?>? PaceSecondsPerKm, List<decimal?>? GradePercent,
        List<double?>? Latitude, List<double?>? Longitude, string Source, bool IsDownsampled);

    private const string Header =
        "ID aktivity,Datum aktivity,Název aktivity,Typ aktivity,Popis aktivity,Uplynulý čas,Vzdálenost,Maximální tepová frekvence,Relativní úsilí,Dojíždění,Soukromá poznámka k aktivitě,Vybavení na aktivitu,Název souboru,Hmotnost sportovce,Hmotnost kola,"
        + "Uplynulý čas,Aktivní čas,Vzdálenost,Maximální rychlost,Průměrná rychlost,Nastoupaná výška,Naklesaná výška,Nejnižší nadmořská výška,Nejvyšší nadmořská výška,Maximální sklon,Průměrný sklon,Průměrný pozitivní sklon,Průměrný záporný sklon,Maximální kadence,Průměrná kadence,Maximální tepová frekvence,Průměrná tepová frekvence,Maximální výkon ve wattech,Průměrný výkon ve wattech,Kalorie";

    /// <summary>600 s run as GPX with heart rate + cadence extensions, heading north at 3 m/s.</summary>
    private static string Gpx(DateTime start, int seconds)
    {
        var sb = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx creator="StravaGPX" version="1.1" xmlns="http://www.topografix.com/GPX/1/1" xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
             <trk><type>running</type><trkseg>
            """);
        for (var s = 0; s < seconds; s++)
        {
            var lat = (50.0 + s * 3.0 / 111_195).ToString("F7", CultureInfo.InvariantCulture);
            var ele = (200 + s * 0.1).ToString("F1", CultureInfo.InvariantCulture);
            sb.Append($"""<trkpt lat="{lat}" lon="14.4000000"><ele>{ele}</ele><time>{start.AddSeconds(s):yyyy-MM-ddTHH:mm:ssZ}</time><extensions><gpxtpx:TrackPointExtension><gpxtpx:hr>{140 + s % 10}</gpxtpx:hr><gpxtpx:cad>84</gpxtpx:cad></gpxtpx:TrackPointExtension></extensions></trkpt>""");
        }
        sb.Append("</trkseg></trk></gpx>");
        return sb.ToString();
    }

    /// <summary>A 3-hour 1 Hz indoor ride as FIT — long enough to be downsampled, with power.</summary>
    private static byte[] Fit(DateTime start, int seconds)
    {
        using var buffer = new MemoryStream();
        var encoder = new Encode(ProtocolVersion.V20);
        encoder.Open(buffer);

        var fileId = new FileIdMesg();
        fileId.SetType(Dynastream.Fit.File.Activity);
        fileId.SetManufacturer(Manufacturer.Garmin);
        fileId.SetSerialNumber(3602364024);
        fileId.SetTimeCreated(new Dynastream.Fit.DateTime(start));
        encoder.Write(fileId);

        for (var s = 0; s < seconds; s++)
        {
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(start.AddSeconds(s)));
            record.SetHeartRate((byte)(120 + s % 30));
            record.SetPower((ushort)(200 + s % 50));
            record.SetCadence(90);
            record.SetDistance(s * 8f);
            encoder.Write(record);
        }

        var session = new SessionMesg();
        session.SetStartTime(new Dynastream.Fit.DateTime(start));
        session.SetSport(Sport.Cycling);
        encoder.Write(session);
        encoder.Close();
        return buffer.ToArray();
    }

    private static string Date(DateTime utc) => utc.ToString("d. M. yyyy H:mm:ss", CultureInfo.GetCultureInfo("cs-CZ"));

    private static byte[] BuildArchive()
    {
        var csv = new StringBuilder(Header).Append('\n')
            .Append($"5001,\"{Date(RunStart)}\",Ranní běh,Běh,,600,\"1,80\",,,,,,activities/6001.gpx.gz,,,600.0,600.0,1800.0,,3.0,60.0,,,,,,,,,,,,,,\n")
            .Append($"5002,\"{Date(RideStart)}\",Trenažér,Virtuální jízda,,10800,\"86,40\",,,,,,activities/6002.fit.gz,,,10800.0,10800.0,86400.0,,8.0,,,,,,,,,,,,,,,\n")
            .Append($"5003,\"{Date(OldRunStart)}\",Starší běh,Běh,,600,\"1,80\",,,,,,activities/6003.gpx,,,600.0,600.0,1800.0,,3.0,60.0,,,,,,,,,,,,,,\n");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "activities.csv", Encoding.UTF8.GetBytes(csv.ToString()));
            Add(zip, "activities/6001.gpx.gz", Gzip(Encoding.UTF8.GetBytes(Gpx(RunStart, 600))));
            Add(zip, "activities/6002.fit.gz", Gzip(Fit(RideStart, 3 * 3600)));
            Add(zip, "activities/6003.gpx", Encoding.UTF8.GetBytes(Gpx(OldRunStart, 600)));
        }
        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] content)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(content);
    }

    private static byte[] Gzip(byte[] content)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(content);
        }
        return buffer.ToArray();
    }

    private static async Task<ImportDto> WaitForAsync(HttpClient client, Guid id, string status)
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var dto = (await client.GetFromJsonAsync<ImportDto>($"{Route}/{id}", JsonOptions))!;
            if (dto.Status == status) return dto;
            if (dto.Status == "Failed") throw new Xunit.Sdk.XunitException($"Import selhal: {dto.ErrorMessage}");
            await Task.Delay(100);
        }
        throw new TimeoutException();
    }

    private static async Task<(ImportDto Preview, ImportDto Done)> ImportAsync(HttpClient client)
    {
        var file = new ByteArrayContent(BuildArchive());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        var created = await client.PostAsync($"{Route}/upload", new MultipartFormDataContent { { file, "file", "export_42.zip" } });
        var id = (await created.Content.ReadFromJsonAsync<ImportDto>(JsonOptions))!.Id;
        var preview = await WaitForAsync(client, id, "PreviewReady");
        (await client.PostAsync($"{Route}/{id}/confirm", null)).EnsureSuccessStatusCode();
        return (preview, await WaitForAsync(client, id, "Succeeded"));
    }

    [Fact]
    public async Task Import_stores_streams_serves_them_with_gps_and_backfills_existing_activities()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        var userId = Guid.Parse(auth.UserId);

        // Already synced from the live Strava API before the import — no stream stored for it.
        Guid existingId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = "5003", FetchedAtUtc = DateTime.UtcNow };
            var existing = new CompletedActivity
            {
                AthleteUserId = userId, Sport = SportType.Running, Title = "Starší běh", StartedAtUtc = OldRunStart,
                DurationSeconds = 600, DistanceMeters = 1800, CreatedAtUtc = DateTime.UtcNow,
                PrimarySourceRecordId = record.Id, SourceRecords = { record },
            };
            db.CompletedActivities.Add(existing);
            await db.SaveChangesAsync();
            existingId = existing.Id;
        }

        var (preview, done) = await ImportAsync(client);

        preview.PreviewStreamsToAdd.Should().Be(3);
        done.ItemsCreated.Should().Be(2);
        done.ItemsSkippedDuplicate.Should().Be(1);
        done.ItemsStreamsAdded.Should().Be(3, "the already-existing activity gets its stream too");

        Guid runId, rideId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            runId = await db.CompletedActivities.Where(a => a.AthleteUserId == userId && a.StartedAtUtc == RunStart).Select(a => a.Id).SingleAsync();
            rideId = await db.CompletedActivities.Where(a => a.AthleteUserId == userId && a.StartedAtUtc == RideStart).Select(a => a.Id).SingleAsync();
        }

        var run = (await client.GetFromJsonAsync<StreamsDto>($"/api/activities/{runId}/streams", JsonOptions))!;
        run.Source.Should().Be("Stored");
        run.IsDownsampled.Should().BeFalse();
        run.TimeOffsetsSeconds.Should().HaveCount(600);
        run.Latitude![0].Should().BeApproximately(50.0, 1e-6);
        run.Longitude!.Should().OnlyContain(l => l != null && Math.Abs(l.Value - 14.4) < 1e-6);
        run.HeartRateBpm![5].Should().Be(145);
        run.CadenceRpm![0].Should().Be(168, "running cadence per leg (84) is reported as steps per minute");
        run.DistanceMeters![^1].Should().BeApproximately(1797m, 2m, "derived from GPS");
        run.PaceSecondsPerKm![300].Should().BeInRange(330, 337, "3 m/s ≈ 5:33 /km");
        run.GradePercent![300].Should().BeApproximately(3.3m, 0.2m, "0.1 m up per 3 m");

        var ride = (await client.GetFromJsonAsync<StreamsDto>($"/api/activities/{rideId}/streams", JsonOptions))!;
        ride.IsDownsampled.Should().BeTrue();
        ride.TimeOffsetsSeconds.Count.Should().BeLessThanOrEqualTo(2000);
        ride.PowerWatts!.Should().OnlyContain(p => p >= 200 && p <= 249);
        ride.CadenceRpm![0].Should().Be(90, "cycling cadence stays as recorded");
        ride.Latitude.Should().BeNull("indoor ride without GPS");

        var backfilled = (await client.GetFromJsonAsync<StreamsDto>($"/api/activities/{existingId}/streams", JsonOptions))!;
        backfilled.Source.Should().Be("Stored");

        var (again, againDone) = await ImportAsync(client);
        again.PreviewStreamsToAdd.Should().Be(0);
        againDone.ItemsStreamsAdded.Should().Be(0);
    }

    [Fact]
    public async Task Another_athletes_stream_is_not_accessible()
    {
        var owner = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var other = AuthenticatedClient(await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"));
        await ImportAsync(AuthenticatedClient(owner));

        Guid runId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            runId = await db.CompletedActivities.Where(a => a.AthleteUserId == Guid.Parse(owner.UserId)).Select(a => a.Id).FirstAsync();
        }

        (await other.GetAsync($"/api/activities/{runId}/streams")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    private record PersonalBestDto(string Sport, string Type, decimal Value, Guid ActivityId, bool IsPrecise, int EffortCount, List<StepDto> Progression);
    private record StepDto(DateTime AchievedAtUtc, decimal Value, Guid ActivityId);
    private record EffortDto(string Type, decimal Value, bool IsPrecise, int Rank, bool IsPersonalBest);

    [Fact]
    public async Task Import_computes_precise_best_efforts_and_personal_bests_with_progression()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);

        await ImportAsync(client);

        var bests = (await client.GetFromJsonAsync<List<PersonalBestDto>>($"/api/athletes/{auth.UserId}/personal-bests", JsonOptions))!;
        var km = bests.Single(b => b.Sport == "Running" && b.Type == "Distance1Km");
        km.Value.Should().BeApproximately(333m, 2m, "1000 m at 3 m/s");
        km.IsPrecise.Should().BeTrue("computed from the full file during the import");
        km.EffortCount.Should().Be(2, "two runs in the archive");
        km.Progression.Should().HaveCount(1, "both runs are equally fast — the later one doesn't beat the first");
        km.Progression[0].AchievedAtUtc.Should().Be(OldRunStart);

        var power = bests.Single(b => b.Sport == "Cycling" && b.Type == "Power20Min");
        power.Value.Should().BeInRange(220m, 230m, "200–249 W cycling sawtooth");
        bests.Should().NotContain(b => b.Sport == "Cycling" && b.Type.StartsWith("Distance"), "distance efforts are for runs only");

        var efforts = (await client.GetFromJsonAsync<List<EffortDto>>($"/api/activities/{km.ActivityId}/best-efforts", JsonOptions))!;
        efforts.Single(e => e.Type == "Distance1Km").Should().Match<EffortDto>(e => e.Rank == 1 && e.IsPersonalBest);
    }

    [Fact]
    public async Task Background_pass_computes_estimates_for_streams_stored_without_efforts()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        Guid activityId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var builder = new TrainCoach.Application.Execution.Streams.ActivityStreamBuilder();
            for (var s = 0; s <= 400; s++)
            {
                builder.Add(new TrainCoach.Application.Execution.Streams.ActivityStreamBuilder.Sample(RunStart.AddSeconds(s), Distance: s * 4.0));
            }
            var record = new ActivitySourceRecord { Source = DataSource.Strava, ExternalId = $"{auth.UserId}-old", FetchedAtUtc = DateTime.UtcNow };
            var activity = new CompletedActivity
            {
                AthleteUserId = Guid.Parse(auth.UserId), Sport = SportType.Running, StartedAtUtc = RunStart, DurationSeconds = 400,
                CreatedAtUtc = DateTime.UtcNow, PrimarySourceRecordId = record.Id, SourceRecords = { record },
            };
            db.CompletedActivities.Add(activity);
            db.ActivityStreams.Add(TrainCoach.Application.Execution.Streams.ActivityStreamMapping.ToEntity(record.Id, builder.Build()!, ActivityStreamOrigin.StravaArchive, DateTime.UtcNow));
            await db.SaveChangesAsync();
            activityId = activity.Id;
        }

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TrainCoach.Application.Execution.IBestEffortRecomputeJob>().RunAsync(Guid.Parse(auth.UserId));
        }

        var efforts = (await client.GetFromJsonAsync<List<EffortDto>>($"/api/activities/{activityId}/best-efforts", JsonOptions))!;
        efforts.Single(e => e.Type == "Distance1Km").Should().Match<EffortDto>(e => e.Value == 250m && !e.IsPrecise);
    }
}
