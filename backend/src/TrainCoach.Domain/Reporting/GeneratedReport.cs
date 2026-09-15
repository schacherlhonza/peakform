using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Reporting;

/// <summary>
/// A generated morning/evening report. The pipeline that produces this (metric calculation ->
/// rule engine -> text generation -> delivery) is kept as separate application services; this
/// entity only stores the pipeline's *output*, plus which rules fired (<see cref="Insights"/>)
/// so a coach dashboard can query "who needs attention today" without re-parsing free text.
/// Rule-based and explanatory only — never a medical diagnosis, and it never edits a plan.
/// </summary>
public class GeneratedReport : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public ReportType Type { get; set; }
    public DateTime GeneratedAtUtc { get; set; }

    public string NarrativeText { get; set; } = string.Empty;

    public ReportDeliveryChannel DeliveryChannel { get; set; } = ReportDeliveryChannel.InApp;
    public ReportDeliveryStatus DeliveryStatus { get; set; } = ReportDeliveryStatus.Pending;
    public DateTime? DeliveredAtUtc { get; set; }

    public ICollection<GeneratedReportInsight> Insights { get; set; } = new List<GeneratedReportInsight>();
}

public class GeneratedReportInsight : Entity
{
    public Guid GeneratedReportId { get; set; }
    public GeneratedReport GeneratedReport { get; set; } = null!;

    public string RuleCode { get; set; } = string.Empty;
    public InsightSeverity Severity { get; set; } = InsightSeverity.Info;
    public string Message { get; set; } = string.Empty;
}
