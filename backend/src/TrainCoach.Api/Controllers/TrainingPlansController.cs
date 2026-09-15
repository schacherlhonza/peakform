using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TrainingPlansController(ITrainingPlanService service, ICurrentUserService currentUser) : ControllerBase
{
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
