using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class HeartRateZonesController(IHeartRateZoneService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/heart-rate-zones")]
    public async Task<ActionResult<IReadOnlyList<HeartRateZoneDto>>> GetForAthlete(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpPut("athletes/{athleteUserId:guid}/heart-rate-zones")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<IReadOnlyList<HeartRateZoneDto>>> SetZones(Guid athleteUserId, SetHeartRateZonesRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        return Ok(await service.SetZonesAsync(currentUser.UserId, request, cancellationToken));
    }
}
