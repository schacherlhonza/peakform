using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainCoach.Api.IntegrationTests.Infrastructure;
using TrainCoach.Domain.Enums;
using TrainCoach.Infrastructure.Persistence;
using Xunit;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

/// <summary>
/// Strava archive import end to end through the HTTP API and the real background worker:
/// upload → dry-run preview → confirm → activities created; re-import is a no-op thanks to level-1
/// dedup. The archive is synthetic (built here) — never real athlete data.
/// </summary>
public class StravaArchiveImportApiTests : IntegrationTestBase
{
    private const string Route = "/api/integrations/strava/archive-imports";

    private record ImportDto(
        Guid Id, string Status, int? PreviewTotal, int? PreviewInFilter, int? PreviewAlreadyImported, int? PreviewWouldCreate,
        int? PreviewWouldReview, int ItemsCreated, int ItemsMerged, int ItemsSkippedDuplicate, int ItemsFailed, string? ErrorMessage);

    private const string Header =
        "ID aktivity,Datum aktivity,Název aktivity,Typ aktivity,Popis aktivity,Uplynulý čas,Vzdálenost,Maximální tepová frekvence,Relativní úsilí,Dojíždění,Soukromá poznámka k aktivitě,Vybavení na aktivitu,Název souboru,Hmotnost sportovce,Hmotnost kola,"
        + "Uplynulý čas,Aktivní čas,Vzdálenost,Maximální rychlost,Průměrná rychlost,Nastoupaná výška,Naklesaná výška,Nejnižší nadmořská výška,Nejvyšší nadmořská výška,Maximální sklon,Průměrný sklon,Průměrný pozitivní sklon,Průměrný záporný sklon,Maximální kadence,Průměrná kadence,Maximální tepová frekvence,Průměrná tepová frekvence,Maximální výkon ve wattech,Průměrný výkon ve wattech,Kalorie";

    private static byte[] BuildArchive(string? profileAthleteId = null)
    {
        var csv = new StringBuilder(Header).Append('\n')
            // A run with a GPX file…
            .Append("1001,\"3. 3. 2024 7:00:00\",Ranní běh,Běh,\"popis,\nna dva řádky\",1850,\"5,01\",172.0,,,,,activities/2001.gpx.gz,,,1850.0,1800.0,5010.0,,2.78,35.0,,,,,,,,,,172.0,151.0,,,410.0\n")
            // …and two strength sessions 40 minutes apart, no distance: must stay two activities
            // (same source, different ids) rather than being merged by the matcher.
            .Append("1002,\"4. 3. 2024 16:00:00\",Posilování 1,Posilování,,1800,,,,,,,activities/2002.tcx.gz,,,1800.0,1800.0,,,,,,,,,,,,,,,,,,\n")
            .Append("1003,\"4. 3. 2024 16:40:00\",Posilování 2,Posilování,,1800,,,,,,,activities/2003.tcx.gz,,,1800.0,1800.0,,,,,,,,,,,,,,,,,,\n")
            // Outside the date filter used below.
            .Append("1004,\"1. 1. 2020 10:00:00\",Stará jízda,Jízda,,3600,\"30,00\",,,,,,activities/2004.gpx,,,3600.0,3600.0,30000.0,,,,,,,,,,,,,,,,,\n");

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "activities.csv", Encoding.UTF8.GetBytes(csv.ToString()));
            AddEntry(zip, "activities/2001.gpx.gz", Gzip("""
                <?xml version="1.0" encoding="UTF-8"?>
                <gpx creator="StravaGPX" version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
                 <metadata><time>2024-03-03T07:00:00Z</time></metadata>
                 <trk><name>Ranní běh</name><type>running</type><trkseg><trkpt lat="50" lon="14"><time>2024-03-03T07:00:00Z</time></trkpt></trkseg></trk>
                </gpx>
                """));
            AddEntry(zip, "activities/2002.tcx.gz", Gzip("""<?xml version="1.0"?><TrainingCenterDatabase><Activities><Activity Sport="Other"><Id>2024-03-04T16:00:00Z</Id></Activity></Activities></TrainingCenterDatabase>"""));
            // Private data the importer must never need — present only to mirror a real export.
            AddEntry(zip, "messaging.json", Encoding.UTF8.GetBytes("[]"));
            if (profileAthleteId is not null)
            {
                AddEntry(zip, "profile.csv", Encoding.UTF8.GetBytes($"ID sportovce,E-mailová adresa,Jméno\n{profileAthleteId},x@example.com,Test\n"));
            }
        }
        return buffer.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] content)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(content);
    }

    private static byte[] Gzip(string text)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }
        return buffer.ToArray();
    }

    private static MultipartFormDataContent UploadContent(byte[] archive, string? fromDate = null, string fileName = "export_42.zip")
    {
        var file = new ByteArrayContent(archive);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        // Fields before the file — the server streams the upload (StravaArchiveImportsController.Upload).
        var form = new MultipartFormDataContent();
        if (fromDate is not null)
        {
            form.Add(new StringContent(fromDate), "fromDate");
        }
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<ImportDto> WaitForAsync(HttpClient client, Guid id, params string[] statuses)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var dto = (await client.GetFromJsonAsync<ImportDto>($"{Route}/{id}", JsonOptions))!;
            if (statuses.Contains(dto.Status))
            {
                return dto;
            }
            if (dto.Status is "Failed")
            {
                throw new Xunit.Sdk.XunitException($"Import selhal: {dto.ErrorMessage}");
            }
            await Task.Delay(100);
        }
        throw new TimeoutException($"Import {id} nedosáhl stavu {string.Join('/', statuses)}.");
    }

    private async Task<ImportDto> UploadConfirmAndWaitAsync(HttpClient client, byte[] archive)
    {
        var created = await client.PostAsync($"{Route}/upload", UploadContent(archive, fromDate: "2024-01-01"));
        created.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var id = (await created.Content.ReadFromJsonAsync<ImportDto>(JsonOptions))!.Id;

        var preview = await WaitForAsync(client, id, "PreviewReady");
        (await client.PostAsync($"{Route}/{id}/confirm", null)).EnsureSuccessStatusCode();
        var done = await WaitForAsync(client, id, "Succeeded");
        return done with { PreviewTotal = preview.PreviewTotal, PreviewInFilter = preview.PreviewInFilter, PreviewWouldCreate = preview.PreviewWouldCreate, PreviewAlreadyImported = preview.PreviewAlreadyImported };
    }

    [Fact]
    public async Task Upload_preview_confirm_creates_activities_and_reimport_creates_none()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        var archive = BuildArchive();

        var first = await UploadConfirmAndWaitAsync(client, archive);

        first.PreviewTotal.Should().Be(4);
        first.PreviewInFilter.Should().Be(3, "the 2020 ride is before fromDate");
        first.PreviewWouldCreate.Should().Be(3);
        first.ItemsCreated.Should().Be(3);
        first.ItemsMerged.Should().Be(0, "two same-day Strava strength sessions are different activities");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            var userId = Guid.Parse(auth.UserId);
            var activities = await db.CompletedActivities.Include(a => a.SourceRecords)
                .Where(a => a.AthleteUserId == userId).OrderBy(a => a.StartedAtUtc).ToListAsync();

            activities.Select(a => a.Title).Should().Equal("Ranní běh", "Posilování 1", "Posilování 2");
            var run = activities[0];
            run.Sport.Should().Be(SportType.Running);
            run.StartedAtUtc.Should().Be(new DateTime(2024, 3, 3, 7, 0, 0, DateTimeKind.Utc));
            run.DurationSeconds.Should().Be(1800);
            run.DistanceMeters.Should().Be(5010m);
            run.AverageHeartRateBpm.Should().Be(151);
            run.SourceRecords.Single().Source.Should().Be(DataSource.Strava);
            run.SourceRecords.Single().ExternalId.Should().Be("1001", "the Strava activity id, same key the API sync uses");
            run.SourceRecords.Single().StravaArchiveImportId.Should().Be(first.Id);
            activities[1].Sport.Should().Be(SportType.Strength);
        }

        var second = await UploadConfirmAndWaitAsync(client, archive);

        second.PreviewAlreadyImported.Should().Be(3);
        second.ItemsCreated.Should().Be(0);
        second.ItemsSkippedDuplicate.Should().Be(3);
    }

    [Fact]
    public async Task Cancelled_preview_imports_nothing_and_frees_the_slot_for_a_new_import()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);

        var created = await client.PostAsync($"{Route}/upload", UploadContent(BuildArchive()));
        var id = (await created.Content.ReadFromJsonAsync<ImportDto>(JsonOptions))!.Id;
        await WaitForAsync(client, id, "PreviewReady");

        (await client.PostAsync($"{Route}/upload", UploadContent(BuildArchive()))).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "only one active import per athlete");

        (await client.PostAsync($"{Route}/{id}/cancel", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"{Route}/{id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            (await db.CompletedActivities.CountAsync(a => a.AthleteUserId == Guid.Parse(auth.UserId))).Should().Be(0);
        }

        (await client.PostAsync($"{Route}/upload", UploadContent(BuildArchive()))).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Link_outside_the_strava_allow_list_is_rejected_without_fetching()
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);

        var response = await client.PostAsJsonAsync($"{Route}/link", new { url = "https://169.254.169.254/latest/meta-data/export.zip" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonAsync<List<ImportDto>>(Route, JsonOptions)).Should().BeEmpty();
    }

    [Fact]
    public async Task Another_athletes_import_is_not_visible()
    {
        var owner = AuthenticatedClient(await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"));
        var other = AuthenticatedClient(await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"));

        var created = await owner.PostAsync($"{Route}/upload", UploadContent(BuildArchive()));
        var id = (await created.Content.ReadFromJsonAsync<ImportDto>(JsonOptions))!.Id;

        (await other.GetAsync($"{Route}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"{Route}/{id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await WaitForAsync(owner, id, "PreviewReady");
    }

    [Theory]
    [InlineData("999", "Failed")] // renamed archive of another Strava account
    [InlineData("42", "PreviewReady")]
    public async Task Archive_owner_is_checked_against_profile_csv_not_the_file_name(string profileAthleteId, string expectedStatus)
    {
        var auth = await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete");
        var client = AuthenticatedClient(auth);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrainCoachDbContext>();
            db.IntegrationConnections.Add(new TrainCoach.Domain.Integrations.IntegrationConnection
            {
                AthleteUserId = Guid.Parse(auth.UserId), Provider = IntegrationProviderType.Strava, ExternalAccountId = "42",
                Status = IntegrationConnectionStatus.Connected, ConnectedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var created = await client.PostAsync($"{Route}/upload", UploadContent(BuildArchive(profileAthleteId), fileName: "moje-data.zip"));
        var id = (await created.Content.ReadFromJsonAsync<ImportDto>(JsonOptions))!.Id;

        ImportDto dto = null!;
        for (var attempt = 0; attempt < 150; attempt++)
        {
            dto = (await client.GetFromJsonAsync<ImportDto>($"{Route}/{id}", JsonOptions))!;
            if (dto.Status is "Failed" or "PreviewReady") break;
            await Task.Delay(100);
        }

        dto.Status.Should().Be(expectedStatus);
        if (expectedStatus == "Failed")
        {
            dto.ErrorMessage.Should().Contain("jinému účtu Strava");
        }
    }

    [Fact]
    public async Task Streamed_upload_applies_fields_sent_before_the_file_and_rejects_bad_requests()
    {
        var client = AuthenticatedClient(await RegisterAsync($"{Guid.NewGuid():N}@test.cz", "Athlete"));

        var noFile = new MultipartFormDataContent { { new StringContent("2024-01-01"), "fromDate" } };
        (await client.PostAsync($"{Route}/upload", noFile)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var empty = new ByteArrayContent([]);
        empty.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        (await client.PostAsync($"{Route}/upload", new MultipartFormDataContent { { empty, "file", "export_42.zip" } })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "an empty file is a business-rule error");

        var form = new MultipartFormDataContent
        {
            { new StringContent("2024-01-01"), "fromDate" },
            { new StringContent("Strength"), "sports" },
        };
        var file = new ByteArrayContent(BuildArchive());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", "export_42.zip");
        var created = await client.PostAsync($"{Route}/upload", form);
        created.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var preview = await WaitForAsync(client, (await created.Content.ReadFromJsonAsync<ImportDto>(JsonOptions))!.Id, "PreviewReady");
        preview.PreviewInFilter.Should().Be(2, "only the two 2024 strength sessions match fromDate + sports");
    }
}
