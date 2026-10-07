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

    /// <summary>The athlete sets their own zones (Settings → Heart rate zones); a coach needs the
    /// EditTrainingPlan permission — both enforced by the access guard in the service. (Was
    /// coach-only, which made the athlete's own settings page fail with 403.)</summary>
    [HttpPut("athletes/{athleteUserId:guid}/heart-rate-zones")]
    public async Task<ActionResult<IReadOnlyList<HeartRateZoneDto>>> SetZones(Guid athleteUserId, SetHeartRateZonesRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        return Ok(await service.SetZonesAsync(currentUser.UserId, request, cancellationToken));
    }

    [HttpGet("athletes/{athleteUserId:guid}/thresholds")]
    public async Task<ActionResult<AthleteThresholdsDto>> GetThresholds(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetThresholdsAsync(athleteUserId, cancellationToken));
    }

    /// <summary>Same access as the zones: the athlete, or a coach with EditTrainingPlan.</summary>
    [HttpPut("athletes/{athleteUserId:guid}/thresholds")]
    public async Task<ActionResult<AthleteThresholdsDto>> SetThresholds(Guid athleteUserId, AthleteThresholdsDto request, CancellationToken cancellationToken)
    {
        return Ok(await service.SetThresholdsAsync(athleteUserId, request, cancellationToken));
    }
}
