using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

/// <summary>Self-service only, same trust model as IntegrationConnectionsController — an athlete
/// manages only their own connector policy, never a coach.</summary>
[ApiController]
[Route("api/athletes/{athleteUserId:guid}/connector-policies")]
[Authorize(Roles = "Athlete")]
public class ConnectorPolicyController(IConnectorPolicyService service, ICurrentUserService currentUser) : ControllerBase
{
    public record SetConnectorPolicyRequest(IntegrationProviderType Provider, DataDomain Domain, ConnectorMode Mode);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectorDomainPolicyDto>>> GetForAthlete(Guid athleteUserId, CancellationToken cancellationToken)
    {
        EnsureSelf(athleteUserId);
        return Ok(await service.GetForAthleteAsync(athleteUserId, cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<ConnectorDomainPolicyDto>> SetOverride(Guid athleteUserId, SetConnectorPolicyRequest request, CancellationToken cancellationToken)
    {
        EnsureSelf(athleteUserId);
        return Ok(await service.SetOverrideAsync(athleteUserId, request.Provider, request.Domain, request.Mode, cancellationToken));
    }

    private void EnsureSelf(Guid athleteUserId)
    {
        if (currentUser.UserId != athleteUserId)
        {
            throw new ForbiddenAccessException("Nastavení konektorů může upravovat pouze vlastník účtu.");
        }
    }
}
