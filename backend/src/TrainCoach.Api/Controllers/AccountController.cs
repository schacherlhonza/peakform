using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Account;
using TrainCoach.Application.Common;

namespace TrainCoach.Api.Controllers;

/// <summary>Self-service GDPR data export/erasure — docs/security.md §11.</summary>
[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(
    IAccountExportService exportService,
    IActivityStreamExportService streamExportService,
    IAccountDeletionService deletionService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("export")]
    public async Task<ActionResult<AccountDataExportDto>> Export(CancellationToken cancellationToken)
    {
        var result = await exportService.ExportAsync(currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The stored activity streams as a ZIP — GPX for routes, CSV for the rest. Built into a temp
    /// file first (ZipArchive writes synchronously, which Kestrel forbids on the response) and sent
    /// from there; the file deletes itself when the response is done.
    /// </summary>
    [HttpGet("export/streams")]
    [Produces("application/zip")]
    public async Task<IActionResult> ExportStreams(CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"peakform-streams-{Guid.NewGuid():N}.zip");
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920);
            await streamExportService.WriteZipAsync(currentUser.UserId, file, cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }
        var read = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        return File(read, "application/zip", $"peakform-trasy-{DateTime.UtcNow:yyyy-MM-dd}.zip");
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await deletionService.DeleteMyAccountAsync(currentUser.UserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }
}
