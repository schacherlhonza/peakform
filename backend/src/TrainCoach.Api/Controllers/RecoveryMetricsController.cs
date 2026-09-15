using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class RecoveryMetricsController(IRecoveryMetricService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/recovery")]
    public async Task<ActionResult<IReadOnlyList<RecoveryMetricDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpPut("athletes/{athleteUserId:guid}/recovery")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<RecoveryMetricDto>> CreateOrUpdate(
        Guid athleteUserId, UpsertRecoveryMetricRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        var result = await service.CreateOrUpdateAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }
}
