namespace TrainCoach.Application.Reporting;

public interface IReportRuleEngine
{
    /// <summary>Evaluates every rule against the snapshot and returns the insights that fired,
    /// most severe first. Every rule is transparent and traceable by its RuleCode — this never
    /// calls out to an external model and never produces a medical diagnosis.</summary>
    IReadOnlyList<GeneratedReportInsightDto> Evaluate(ReportInputSnapshot snapshot);
}
