using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Relationships;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/relationships")]
[Authorize]
public class RelationshipsController(ICoachAthleteRelationshipService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CoachAthleteRelationshipDto>>> GetMine(CancellationToken cancellationToken)
    {
        var result = await service.GetMyRelationshipsAsync(currentUser.UserId, currentUser.Role, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invite")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<CoachAthleteRelationshipDto>> Invite(InviteAthleteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.InviteAthleteAsync(currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/respond")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<CoachAthleteRelationshipDto>> Respond(Guid id, RespondToInviteRequest request, CancellationToken cancellationToken)
    {
        var result = await service.RespondToInviteAsync(currentUser.UserId, id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<CoachAthleteRelationshipDto>> Revoke(Guid id, RevokeAccessRequest request, CancellationToken cancellationToken)
    {
        var result = await service.RevokeAsync(currentUser.UserId, id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/permissions")]
    [Authorize(Roles = "Athlete")]
    public async Task<ActionResult<CoachAthleteRelationshipDto>> SetPermission(Guid id, SetPermissionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SetPermissionAsync(currentUser.UserId, id, request, cancellationToken);
        return Ok(result);
    }
}
