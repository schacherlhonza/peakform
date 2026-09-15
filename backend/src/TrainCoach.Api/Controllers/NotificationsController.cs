using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Platform;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class NotificationsController(INotificationService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetOwn(CancellationToken cancellationToken)
    {
        return Ok(await service.GetForUserAsync(currentUser.UserId, cancellationToken));
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await service.MarkReadAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
