using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Coaching;
using TrainCoach.Application.Common;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/coach")]
[Authorize(Roles = "Coach")]
public class CoachController(ICoachTodayService today, ICurrentUserService currentUser, IDateTimeProvider clock) : ControllerBase
{
    /// <summary>Every active athlete's day at a glance — readiness, planned workouts, actual activities,
    /// week progress, active health flags — limited to what each athlete shares.</summary>
    /// <param name="date">The coach's local date; today (UTC) when omitted.</param>
    [HttpGet("today")]
    public async Task<ActionResult<CoachTodayDto>> GetToday([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        return Ok(await today.GetAsync(currentUser.UserId, date ?? DateOnly.FromDateTime(clock.UtcNow), cancellationToken));
    }
}
