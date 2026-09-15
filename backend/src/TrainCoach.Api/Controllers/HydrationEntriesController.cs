using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Nutrition;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class HydrationEntriesController(IHydrationEntryService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/hydration")]
    public async Task<ActionResult<IReadOnlyList<HydrationEntryDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpPost("athletes/{athleteUserId:guid}/hydration")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<HydrationEntryDto>> Create(
        Guid athleteUserId, CreateHydrationEntryRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("athletes/{athleteUserId:guid}/hydration/{entryId:guid}")]
    [Authorize(Roles = "Athlete")]
    public async Task<IActionResult> Delete(Guid athleteUserId, Guid entryId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(currentUser.UserId, entryId, cancellationToken);
        return NoContent();
    }
}
