namespace TrainCoach.Application.Reporting;

public interface IReportNarrativeComposer
{
    string Compose(ReportInputSnapshot snapshot, IReadOnlyList<GeneratedReportInsightDto> insights);
}
