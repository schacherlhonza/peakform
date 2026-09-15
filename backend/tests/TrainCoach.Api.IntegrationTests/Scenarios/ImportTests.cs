using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using TrainCoach.Api.IntegrationTests.Infrastructure;

namespace TrainCoach.Api.IntegrationTests.Scenarios;

public class ImportTests : IntegrationTestBase
{
    private record PreviewRowDto(int RowNumber, string? Status, string[]? Messages);
    private record PreviewDto(string ImportedFileId, int RowsTotal, int RowsValid, int RowsWithErrors, List<PreviewRowDto> Rows);
    private record ConfirmResultDto(string ImportedFileId, int RowsImported, int RowsSkippedDuplicate, int RowsSkippedInvalid);

    private const string Csv = "Datum,Sport,DobaMinuty,VzdalenostKm,Poznamka\n" +
                                "2026-01-06,Běh,45,8,Lehký běh\n" +
                                "2026-01-08,Běh,60,10,Tempo\n" +
                                "neplatnyRadek,,,,\n";

    [Fact]
    public async Task Upload_GeneratesPreview_WithoutCreatingActivities()
    {
        var athlete = await RegisterAsync($"import-{Guid.NewGuid():N}@example.com", "Athlete");
        var client = AuthenticatedClient(athlete);

        var preview = await UploadAsync(client);

        preview.RowsTotal.Should().Be(3);
        preview.RowsValid.Should().Be(2);
        preview.RowsWithErrors.Should().Be(1);

        // Nothing was persisted as training data yet.
        var activitiesResponse = await client.GetAsync($"/api/athletes/{athlete.UserId}/activities");
        var activities = await activitiesResponse.Content.ReadFromJsonAsync<List<object>>(JsonOptions);
        activities.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_CreatesActivities_AndIsIdempotentOnReconfirm()
    {
        var athlete = await RegisterAsync($"import-confirm-{Guid.NewGuid():N}@example.com", "Athlete");
        var client = AuthenticatedClient(athlete);

        var preview = await UploadAsync(client);

        var confirmResponse = await client.PostAsync($"/api/import/{preview.ImportedFileId}/confirm", null);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = (await confirmResponse.Content.ReadFromJsonAsync<ConfirmResultDto>(JsonOptions))!;
        confirmed.RowsImported.Should().Be(2);
        confirmed.RowsSkippedInvalid.Should().Be(1);

        var activitiesResponse = await client.GetAsync($"/api/athletes/{athlete.UserId}/activities");
        var activities = await activitiesResponse.Content.ReadFromJsonAsync<List<object>>(JsonOptions);
        activities.Should().HaveCount(2);

        // Re-confirming the same file must not be possible (already confirmed).
        var secondConfirm = await client.PostAsync($"/api/import/{preview.ImportedFileId}/confirm", null);
        secondConfirm.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Upload_SameContentTwice_ReimportIsDeduplicated()
    {
        var athlete = await RegisterAsync($"import-dedup-{Guid.NewGuid():N}@example.com", "Athlete");
        var client = AuthenticatedClient(athlete);

        var firstPreview = await UploadAsync(client);
        await client.PostAsync($"/api/import/{firstPreview.ImportedFileId}/confirm", null);

        // Upload the exact same CSV content again as a *new* file (e.g. re-exported by mistake)
        // and confirm it — the per-row dedup key (file type + content hash + row) must skip rows
        // already imported instead of creating duplicate activities.
        var secondPreview = await UploadAsync(client);
        var secondConfirmResponse = await client.PostAsync($"/api/import/{secondPreview.ImportedFileId}/confirm", null);
        var secondConfirmed = (await secondConfirmResponse.Content.ReadFromJsonAsync<ConfirmResultDto>(JsonOptions))!;

        secondConfirmed.RowsSkippedDuplicate.Should().Be(2);
        secondConfirmed.RowsImported.Should().Be(0);
    }

    private static async Task<PreviewDto> UploadAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(Csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "trening.csv");
        form.Add(new StringContent("StandardCsvTemplate"), "fileType");

        var response = await client.PostAsync("/api/import/preview", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PreviewDto>(JsonOptions))!;
    }
}
