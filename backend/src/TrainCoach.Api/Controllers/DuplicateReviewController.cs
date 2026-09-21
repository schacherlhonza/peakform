using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Execution;
using TrainCoach.Application.Integrations.Matching;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = "Athlete")]
public class DuplicateReviewController(IDuplicateReviewService service, IDuplicateDryRunReportService dryRunReportService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Generates a fresh dry-run report scoped to the caller's own activities only —
    /// there is no admin role in this app, so a cross-athlete report is out of scope for
    /// self-service (see docs/integrations/canonical-data-and-deduplication-plan.md, Open
    /// Question 1). Writes no MergeDecision/DuplicateCandidate rows.</summary>
    [HttpPost("athletes/{athleteUserId:guid}/duplicate-dry-run-reports")]
    public async Task<ActionResult<DuplicateDryRunReportDto>> GenerateDryRun(Guid athleteUserId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId != athleteUserId)
        {
            return Forbid();
        }
        return Ok(await dryRunReportService.GenerateAsync(athleteUserId, cancellationToken));
    }

    [HttpGet("athletes/{athleteUserId:guid}/duplicate-candidates")]
    public async Task<ActionResult<IReadOnlyList<DuplicateCandidateDto>>> GetPending(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetPendingAsync(currentUser.UserId, athleteUserId, cancellationToken));
    }

    [HttpPost("duplicate-candidates/{id:guid}/merge")]
    public async Task<IActionResult> Merge(Guid id, MergeDuplicateCandidateRequest request, CancellationToken cancellationToken)
    {
        await service.MergeAsync(currentUser.UserId, id, request.SurvivingActivityId, cancellationToken);
        return NoContent();
    }

    [HttpPost("duplicate-candidates/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        await service.DismissAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("merge-decisions/{id:guid}/revert")]
    public async Task<IActionResult> Revert(Guid id, CancellationToken cancellationToken)
    {
        await service.RevertAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpGet("athletes/{athleteUserId:guid}/merge-decisions")]
    public async Task<ActionResult<IReadOnlyList<MergeDecisionDto>>> GetMergeDecisions(Guid athleteUserId, CancellationToken cancellationToken)
    {
        return Ok(await service.GetMergeDecisionsAsync(currentUser.UserId, athleteUserId, cancellationToken));
    }
}
