using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class RacesController(IRaceService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/races")]
    public async Task<ActionResult<IReadOnlyList<RaceDto>>> GetForAthlete(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpPost("races")]
    public async Task<ActionResult<RaceDto>> Create(CreateRaceRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetForAthlete), new { athleteUserId = result.AthleteUserId }, result);
    }

    // Also used to record the actual race result once it has taken place.
    [HttpPut("races/{id:guid}")]
    public async Task<ActionResult<RaceDto>> Update(Guid id, UpdateRaceRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("races/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
