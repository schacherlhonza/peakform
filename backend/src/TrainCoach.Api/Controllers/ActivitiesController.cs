using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ActivitiesController(IActivityService service, IPersonalBestService personalBests, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Personal bests derived from every activity's best efforts, with their progression.</summary>
    [HttpGet("athletes/{athleteUserId:guid}/personal-bests")]
    public async Task<ActionResult<IReadOnlyList<PersonalBestDto>>> GetPersonalBests(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await personalBests.GetForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpGet("activities/{activityId:guid}/best-efforts")]
    public async Task<ActionResult<IReadOnlyList<ActivityBestEffortDto>>> GetBestEfforts(Guid activityId, CancellationToken cancellationToken)
    {
        return Ok(await personalBests.GetForActivityAsync(activityId, cancellationToken));
    }

    [HttpGet("athletes/{athleteUserId:guid}/activities")]
    public async Task<ActionResult<IReadOnlyList<CompletedActivityDto>>> GetForAthlete(
        Guid athleteUserId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(athleteUserId, from, to, cancellationToken));
    }

    /// <summary>Paged activity history with filters — the activities page. <c>pageSize</c> is capped at 100.</summary>
    [HttpGet("athletes/{athleteUserId:guid}/activities/search")]
    public async Task<ActionResult<ActivityListPageDto>> Search(
        Guid athleteUserId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] List<SportType>? sports,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return Ok(await service.SearchForAthleteAsync(athleteUserId, new ActivitySearchQuery(from, to, sports, search, page, pageSize), cancellationToken));
    }

    [HttpGet("activities/{activityId:guid}")]
    public async Task<ActionResult<CompletedActivityDto>> GetById(Guid activityId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetByIdAsync(currentUser.UserId, activityId, cancellationToken));
    }

    [HttpPost("activities")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<CompletedActivityDto>> CreateManual(CreateManualActivityRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateManualAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("activities/{activityId:guid}/streams")]
    public async Task<ActionResult<ActivityStreamsDto>> GetStreams(Guid activityId, CancellationToken cancellationToken)
    {
        var result = await service.GetActivityStreamsAsync(currentUser.UserId, activityId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
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
