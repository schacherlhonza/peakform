using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/workout-templates")]
[Authorize(Roles = "Coach")]
public class WorkoutTemplatesController(IWorkoutTemplateService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkoutTemplateDto>>> GetForCoach(CancellationToken cancellationToken)
    {
        return Ok(await service.GetForCoachAsync(currentUser.UserId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutTemplateDto>> Create(CreateWorkoutTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetForCoach), null, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkoutTemplateDto>> Update(Guid id, UpdateWorkoutTemplateRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateAsync(currentUser.UserId, id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
