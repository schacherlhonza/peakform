using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Reporting;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Api.Controllers;

[ApiController]
[Route("api/athletes/{athleteUserId:guid}/reports")]
[Authorize]
public class ReportsController(IReportGenerationService service) : ControllerBase
{
    /// <summary>Lazily generates the report on first read (e.g. opening the morning dashboard)
    /// rather than requiring a cron job to have run first — see docs/architecture.md.</summary>
    [HttpGet("{date}/{type}")]
    public async Task<ActionResult<GeneratedReportDto>> Get(Guid athleteUserId, DateOnly date, ReportType type, CancellationToken cancellationToken)
    {
        var existing = await service.GetAsync(athleteUserId, type, date, cancellationToken);
        if (existing is not null)
        {
            return Ok(existing);
        }

        var generated = await service.GenerateAsync(athleteUserId, type, date, cancellationToken);
        return Ok(generated);
    }

    [HttpPost("{date}/{type}/regenerate")]
    public async Task<ActionResult<GeneratedReportDto>> Regenerate(Guid athleteUserId, DateOnly date, ReportType type, CancellationToken cancellationToken)
    {
        return Ok(await service.GenerateAsync(athleteUserId, type, date, cancellationToken));
    }
}
