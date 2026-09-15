using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Wellness;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class PersonalRecordsController(IPersonalRecordService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/personal-records")]
    public async Task<ActionResult<IReadOnlyList<PersonalRecordDto>>> GetForAthlete(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpPost("athletes/{athleteUserId:guid}/personal-records")]
    public async Task<ActionResult<PersonalRecordDto>> Create(
        Guid athleteUserId, CreatePersonalRecordRequest request, CancellationToken cancellationToken)
    {
        if (athleteUserId != request.AthleteUserId)
        {
            return BadRequest();
        }

        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("athletes/{athleteUserId:guid}/personal-records/{recordId:guid}")]
    public async Task<ActionResult<PersonalRecordDto>> Update(
        Guid athleteUserId, Guid recordId, UpdatePersonalRecordRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(currentUser.UserId, recordId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("athletes/{athleteUserId:guid}/personal-records/{recordId:guid}")]
    public async Task<IActionResult> Delete(Guid athleteUserId, Guid recordId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(currentUser.UserId, recordId, cancellationToken);
        return NoContent();
    }
}
