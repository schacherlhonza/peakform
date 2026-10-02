using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations.StravaArchive;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

/// <summary>
/// Bulk import of the athlete's Strava "Download your data" archive — see
/// docs/integrations/strava-archive-import.md. Both create endpoints return immediately; the
/// download/analysis runs in the background and the client polls <see cref="Get"/> until the
/// preview is ready, then calls <see cref="Confirm"/>.
/// </summary>
[ApiController]
[Route("api/integrations/strava/archive-imports")]
[Authorize(Roles = "Athlete")]
public class StravaArchiveImportsController(IStravaArchiveImportService service, ICurrentUserService currentUser) : ControllerBase
{
    // Matches StravaArchiveImportOptions.MaxArchiveBytes' default; the service enforces the
    // configured value while streaming, this only lifts Kestrel's 30 MB default for this route.
    private const long MaxUploadBytes = 4L * 1024 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StravaArchiveImportDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await service.ListAsync(currentUser.UserId, cancellationToken));
    }

    [HttpGet("{importId:guid}")]
    public async Task<ActionResult<StravaArchiveImportDto>> Get(Guid importId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAsync(currentUser.UserId, importId, cancellationToken));
    }

    /// <summary>Server-side download from the link in Strava's email (valid 7 days).</summary>
    [HttpPost("link")]
    public async Task<ActionResult<StravaArchiveImportDto>> CreateFromLink(CreateStravaArchiveImportFromLinkRequest request, CancellationToken cancellationToken)
    {
        return Accepted(await service.CreateFromLinkAsync(currentUser.UserId, request, cancellationToken));
    }

    /// <summary>
    /// Multipart upload, streamed straight into the archive store — no <c>IFormFile</c>, which
    /// would first buffer a multi-GB archive into ASP.NET's own temp file and then get copied again.
    /// The optional fields <c>fromDate</c>, <c>toDate</c>, <c>sports</c> (repeatable) must come
    /// <b>before</b> the <c>file</c> part — the import starts as soon as the file part begins.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [DisableFormValueModelBinding]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<StravaArchiveImportDto>> Upload(CancellationToken cancellationToken)
    {
        var boundary = MediaTypeHeaderValue.TryParse(Request.ContentType, out var contentType)
            ? HeaderUtilities.RemoveQuotes(contentType.Boundary).Value
            : null;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            return BadRequest("Očekává se multipart/form-data s ZIP souborem.");
        }

        DateOnly? fromDate = null, toDate = null;
        var sports = new List<SportType>();
        var reader = new MultipartReader(boundary, Request.Body);
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition))
            {
                continue;
            }
            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

            if (disposition.IsFileDisposition() && name == "file")
            {
                var fileName = HeaderUtilities.RemoveQuotes(disposition.FileName).Value
                    ?? HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
                    ?? "archive.zip";
                var result = await service.CreateFromUploadAsync(
                    currentUser.UserId, fileName, section.Body, fromDate, toDate, sports.Count > 0 ? sports : null, cancellationToken);
                return Accepted(result);
            }

            if (disposition.IsFormDisposition())
            {
                // Field values are tiny; cap the read so a malformed request can't buffer much.
                using var fieldReader = new StreamReader(section.Body);
                var buffer = new char[256];
                var length = await fieldReader.ReadBlockAsync(buffer, cancellationToken);
                var value = new string(buffer, 0, length).Trim();
                switch (name)
                {
                    case "fromDate" when DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var from):
                        fromDate = from;
                        break;
                    case "toDate" when DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var to):
                        toDate = to;
                        break;
                    case "sports" when Enum.TryParse<SportType>(value, ignoreCase: true, out var sport):
                        sports.Add(sport);
                        break;
                }
            }
        }

        return BadRequest("V požadavku chybí soubor (pole „file“).");
    }

    [HttpPost("{importId:guid}/confirm")]
    public async Task<ActionResult<StravaArchiveImportDto>> Confirm(Guid importId, CancellationToken cancellationToken)
    {
        return Ok(await service.ConfirmAsync(currentUser.UserId, importId, cancellationToken));
    }

    [HttpPost("{importId:guid}/cancel")]
    public async Task<ActionResult<StravaArchiveImportDto>> Cancel(Guid importId, CancellationToken cancellationToken)
    {
        return Ok(await service.CancelAsync(currentUser.UserId, importId, cancellationToken));
    }
}

/// <summary>Keeps MVC from reading the multipart body into form values before the action streams it.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;
        factories.RemoveType<FormValueProviderFactory>();
        factories.RemoveType<FormFileValueProviderFactory>();
        factories.RemoveType<JQueryFormValueProviderFactory>();
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
