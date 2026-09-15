using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Roles = "Athlete")]
public class ImportController(IImportService service, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Parses and validates the upload; nothing is saved as training data until
    /// <see cref="Confirm"/> is called — see docs/architecture.md, import flow.</summary>
    [HttpPost("preview")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ImportPreviewDto>> Preview(IFormFile file, [FromForm] ImportFileType fileType, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest("Soubor je prázdný.");
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(cancellationToken);

        var result = await service.UploadAndPreviewAsync(currentUser.UserId, currentUser.UserId, file.FileName, content, fileType, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{importedFileId:guid}/confirm")]
    public async Task<ActionResult<ImportConfirmResultDto>> Confirm(Guid importedFileId, CancellationToken cancellationToken)
    {
        return Ok(await service.ConfirmAsync(currentUser.UserId, importedFileId, cancellationToken));
    }
}
