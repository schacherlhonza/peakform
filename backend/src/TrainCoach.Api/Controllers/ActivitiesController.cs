using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ActivitiesController(IActivityService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/activities")]
    public async Task<ActionResult<IReadOnlyList<CompletedActivityDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpPost("activities")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<CompletedActivityDto>> CreateManual(CreateManualActivityRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateManualAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("athletes/{athleteUserId:guid}/feedback")]
    public async Task<ActionResult<IReadOnlyList<TrainingFeedbackDto>>> GetFeedback(
        Guid athleteUserId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetFeedbackForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    [HttpPut("feedback")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<TrainingFeedbackDto>> UpsertFeedback(UpsertTrainingFeedbackRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpsertFeedbackAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }
}
