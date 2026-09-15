using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Reporting;

public record GeneratedReportInsightDto(string RuleCode, InsightSeverity Severity, string Message);

public record GeneratedReportDto(
    Guid Id,
    Guid AthleteUserId,
    DateOnly Date,
    ReportType Type,
    DateTime GeneratedAtUtc,
    string NarrativeText,
    ReportDeliveryChannel DeliveryChannel,
    ReportDeliveryStatus DeliveryStatus,
    IReadOnlyList<GeneratedReportInsightDto> Insights);
