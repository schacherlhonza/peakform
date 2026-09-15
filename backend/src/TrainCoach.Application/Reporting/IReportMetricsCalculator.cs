namespace TrainCoach.Application.Reporting;

public interface IReportMetricsCalculator
{
    Task<ReportInputSnapshot> CalculateAsync(Guid athleteUserId, Domain.Enums.ReportType type, DateOnly date, CancellationToken cancellationToken = default);
}
