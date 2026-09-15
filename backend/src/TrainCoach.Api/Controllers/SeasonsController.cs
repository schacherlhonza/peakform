using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class SeasonsController(ISeasonService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/seasons")]
    public async Task<ActionResult<IReadOnlyList<SeasonDto>>> GetForAthlete(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpPost("seasons")]
    public async Task<ActionResult<SeasonDto>> Create(CreateSeasonRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetForAthlete), new { athleteUserId = result.AthleteUserId }, result);
    }

    [HttpPut("seasons/{id:guid}")]
    public async Task<ActionResult<SeasonDto>> Update(Guid id, UpdateSeasonRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("seasons/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
