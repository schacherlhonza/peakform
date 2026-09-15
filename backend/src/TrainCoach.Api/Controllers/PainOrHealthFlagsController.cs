using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class PainOrHealthFlagsController(IPainOrHealthFlagService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/health-flags")]
    public async Task<ActionResult<IReadOnlyList<PainOrHealthFlagDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, activeOnly, cancellationToken));
    }

    [HttpPost("athletes/{athleteUserId:guid}/health-flags")]
    public async Task<ActionResult<PainOrHealthFlagDto>> Create(
        Guid athleteUserId, CreatePainOrHealthFlagRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("athletes/{athleteUserId:guid}/health-flags/{flagId:guid}/status")]
    public async Task<ActionResult<PainOrHealthFlagDto>> UpdateStatus(
        Guid athleteUserId, Guid flagId, UpdateHealthFlagStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateStatusAsync(currentUser.UserId, flagId, request, cancellationToken);
        return Ok(result);
    }
}
