using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Wellness;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CheckInsController(ICheckInService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPut("checkins")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<DailyCheckInDto>> Submit(SubmitCheckInRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SubmitAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("athletes/{athleteUserId:guid}/checkins/{date}/{type}")]
    public async Task<ActionResult<DailyCheckInDto>> Get(Guid athleteUserId, DateOnly date, CheckInType type, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(athleteUserId, date, type, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("athletes/{athleteUserId:guid}/checkins")]
    public async Task<ActionResult<IReadOnlyList<DailyCheckInDto>>> GetRange(
        Guid athleteUserId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetRangeAsync(athleteUserId, from, to, cancellationToken));
    }
}
