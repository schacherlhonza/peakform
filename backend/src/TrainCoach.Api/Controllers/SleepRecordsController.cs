using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class SleepRecordsController(ISleepRecordService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/sleep")]
    public async Task<ActionResult<IReadOnlyList<SleepRecordDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpPut("athletes/{athleteUserId:guid}/sleep")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<SleepRecordDto>> CreateOrUpdate(
        Guid athleteUserId, UpsertSleepRecordRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        var result = await service.CreateOrUpdateAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }
}
