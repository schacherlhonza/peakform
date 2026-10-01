using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Wellness;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TrainingLoadController(ITrainingLoadService service) : ControllerBase
{
    [HttpGet("athletes/{athleteUserId:guid}/training-load")]
    public async Task<ActionResult<IReadOnlyList<TrainingLoadSnapshotDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] DataSource? source, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, source, cancellationToken));
    }
}
