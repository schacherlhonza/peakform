using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ReadinessController(IReadinessService service) : ControllerBase
{
    /// <summary><c>date</c> is the athlete's local "today" — the result may describe an earlier
    /// day (see <see cref="ReadinessDto.IsToday"/>) when today has no recovery data yet.</summary>
    [HttpGet("athletes/{athleteUserId:guid}/readiness")]
    public async Task<ActionResult<ReadinessDto>> GetForAthlete(Guid athleteUserId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, date, cancellationToken));
    }
}
