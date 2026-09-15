using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Common;
using TrainCoach.Application.Planning;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/abbreviations")]
[Authorize(Roles = "Coach")]
public class CustomAbbreviationsController(ICustomAbbreviationService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomAbbreviationDto>>> GetForCoach(CancellationToken cancellationToken)
    {
        return Ok(await service.GetForCoachAsync(currentUser.UserId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CustomAbbreviationDto>> Create(CreateCustomAbbreviationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetForCoach), null, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomAbbreviationDto>> Update(Guid id, UpdateCustomAbbreviationRequest request, CancellationToken cancellationToken)
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
