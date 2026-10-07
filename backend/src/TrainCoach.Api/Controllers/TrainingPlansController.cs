using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TrainingPlansController(ITrainingPlanService service, IPlanVsActualService planVsActual, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Planned workouts next to what was actually done (duration, distance, time in zones), up to 62 days.</summary>
    [HttpGet("athletes/{athleteUserId:guid}/plan-vs-actual")]
    public async Task<ActionResult<PlanVsActualDto>> GetPlanVsActual(Guid athleteUserId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        return Ok(await planVsActual.GetAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpGet("athletes/{athleteUserId:guid}/plans")]
    public async Task<ActionResult<IReadOnlyList<TrainingPlanDto>>> GetForAthlete(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetPlansForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpGet("plans/{id:guid}")]
    public async Task<ActionResult<TrainingPlanDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetPlanDetailAsync(id, cancellationToken));
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<TrainingPlanDto>> Create(CreateTrainingPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreatePlanAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { id = result.Id }, result);
    }

    [HttpPost("plans/{planId:guid}/weeks")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<TrainingWeekDto>> CreateWeek(Guid planId, CreateTrainingWeekRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.CreateWeekAsync(planId, request, cancellationToken));
    }

    [HttpPut("weeks/{weekId:guid}")]
    public async Task<ActionResult<TrainingWeekDto>> UpdateWeek(Guid weekId, UpdateTrainingWeekRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateWeekAsync(weekId, currentUser.Role, request, cancellationToken));
    }

    [HttpGet("workouts/{id:guid}")]
    public async Task<ActionResult<PlannedWorkoutDto>> GetWorkout(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetWorkoutAsync(id, cancellationToken));
    }

    /// <summary>Whether the workout made it to the athlete's external calendar (intervals.icu → Garmin).</summary>
    [HttpGet("workouts/{id:guid}/push-status")]
    public async Task<ActionResult<IReadOnlyList<WorkoutPushStatusDto>>> GetWorkoutPushStatus(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await service.GetWorkoutPushStatusAsync(id, cancellationToken));
    }

    [HttpPost("workouts")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<PlannedWorkoutDto>> CreateWorkout(CreatePlannedWorkoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.CreateWorkoutAsync(request, cancellationToken));
    }

    [HttpPut("workouts/{id:guid}")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<PlannedWorkoutDto>> UpdateWorkout(Guid id, UpdatePlannedWorkoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateWorkoutAsync(id, request, cancellationToken));
    }

    [HttpDelete("workouts/{id:guid}")]
    [Authorize(Roles = "Coach")]
    public async Task<IActionResult> DeleteWorkout(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteWorkoutAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("workouts/{id:guid}/copy")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<PlannedWorkoutDto>> CopyWorkout(Guid id, CopyWorkoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.CopyWorkoutAsync(id, request, cancellationToken));
    }
}
