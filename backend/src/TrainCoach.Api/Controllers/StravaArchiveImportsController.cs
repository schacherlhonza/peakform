using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<ActionResult<StravaArchiveImportDto>> Upload(
        IFormFile file,
        [FromForm] DateOnly? fromDate,
        [FromForm] DateOnly? toDate,
        [FromForm] List<SportType>? sports,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest("Soubor je prázdný.");
        }

        await using var stream = file.OpenReadStream();
        var result = await service.CreateFromUploadAsync(currentUser.UserId, file.FileName, stream, fromDate, toDate, sports, cancellationToken);
        return Accepted(result);
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
