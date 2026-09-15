using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Reporting;

public interface IReportGenerationService
{
    /// <summary>Generates (or regenerates) the report for a given athlete/date/type: calculate
    /// metrics -> evaluate rules -> compose narrative -> persist -> deliver. Idempotent per
    /// (athlete, date, type) — calling it again replaces the previous report for that key.</summary>
    Task<GeneratedReportDto> GenerateAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default);

    Task<GeneratedReportDto?> GetAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default);
}
