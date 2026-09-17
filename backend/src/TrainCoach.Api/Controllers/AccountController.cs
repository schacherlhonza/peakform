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
    IAccountDeletionService deletionService,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("export")]
    public async Task<ActionResult<AccountDataExportDto>> Export(CancellationToken cancellationToken)
    {
        var result = await exportService.ExportAsync(currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        await deletionService.DeleteMyAccountAsync(currentUser.UserId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }
}
