using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Nutrition;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class FoodEntriesController(IFoodEntryService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/food")]
    public async Task<ActionResult<IReadOnlyList<FoodEntryDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpPost("athletes/{athleteUserId:guid}/food")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<FoodEntryDto>> Create(
        Guid athleteUserId, CreateFoodEntryRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("athletes/{athleteUserId:guid}/food/{entryId:guid}")]
    [Authorize(Roles = "Athlete")]
    public async Task<IActionResult> Delete(Guid athleteUserId, Guid entryId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(currentUser.UserId, entryId, cancellationToken);
        return NoContent();
    }
}
