using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Integrations;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize(Roles = "Athlete")]
public class IntegrationConnectionsController(IIntegrationConnectionService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationConnectionDto>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await service.GetForAthleteAsync(currentUser.UserId, currentUser.UserId, cancellationToken));
    }

    [HttpGet("{provider}/authorize-url")]
    public async Task<ActionResult<AuthorizationUrlDto>> GetAuthorizeUrl(IntegrationProviderType provider, CancellationToken cancellationToken)
    {
        return Ok(await service.GetAuthorizationUrlAsync(currentUser.UserId, provider, cancellationToken));
    }

    public record OAuthCallbackRequest(string State, string Code);

    [HttpPost("{provider}/callback")]
    public async Task<ActionResult<IntegrationConnectionDto>> HandleCallback(IntegrationProviderType provider, OAuthCallbackRequest request, CancellationToken cancellationToken)
    {
        return Ok(await service.HandleOAuthCallbackAsync(currentUser.UserId, provider, request.State, request.Code, cancellationToken));
    }

    [HttpPost("{provider}/connect-demo")]
    public async Task<ActionResult<IntegrationConnectionDto>> ConnectDemo(IntegrationProviderType provider, CancellationToken cancellationToken)
    {
        return Ok(await service.ConnectMockProviderAsync(currentUser.UserId, provider, cancellationToken));
    }

    [HttpDelete("{provider}")]
    public async Task<IActionResult> Disconnect(IntegrationProviderType provider, CancellationToken cancellationToken)
    {
        await service.DisconnectAsync(currentUser.UserId, provider, cancellationToken);
        return NoContent();
    }

    [HttpPost("{provider}/sync")]
    public async Task<IActionResult> TriggerSync(IntegrationProviderType provider, CancellationToken cancellationToken)
    {
        await service.TriggerSyncAsync(currentUser.UserId, provider, cancellationToken);
        return Accepted();
    }

    [HttpGet("{provider}/sync-history")]
    public async Task<ActionResult<IReadOnlyList<SynchronizationRunDto>>> GetSyncHistory(IntegrationProviderType provider, CancellationToken cancellationToken)
    {
        return Ok(await service.GetSyncHistoryAsync(currentUser.UserId, provider, cancellationToken));
    }
}
