using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class CommentsController(ICommentService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("workouts/{workoutId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetForWorkout(Guid workoutId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForWorkoutAsync(workoutId, cancellationToken));
    }

    [HttpPost("comments")]
    public async Task<ActionResult<CommentDto>> Add(CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var authorRole = currentUser.Role == AppRole.Coach ? CommentAuthorRole.Coach : CommentAuthorRole.Athlete;
        var result = await service.AddAsync(currentUser.UserId, authorRole, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("comments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
