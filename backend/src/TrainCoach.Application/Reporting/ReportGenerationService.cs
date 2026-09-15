using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Reporting;

namespace TrainCoach.Application.Reporting;

public class ReportGenerationService(
    IApplicationDbContext db,
    IReportMetricsCalculator calculator,
    IReportRuleEngine ruleEngine,
    IReportNarrativeComposer narrativeComposer,
    IReportDeliveryDispatcher deliveryDispatcher,
    IRelationshipAccessGuard accessGuard,
    IDateTimeProvider clock) : IReportGenerationService
{
    public async Task<GeneratedReportDto> GenerateAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var snapshot = await calculator.CalculateAsync(athleteUserId, type, date, cancellationToken);
        var insights = ruleEngine.Evaluate(snapshot);
        var narrative = narrativeComposer.Compose(snapshot, insights);

        var existing = await db.GeneratedReports
            .Include(r => r.Insights)
            .FirstOrDefaultAsync(r => r.AthleteUserId == athleteUserId && r.Date == date && r.Type == type, cancellationToken);

        if (existing is not null)
        {
            db.GeneratedReportInsights.RemoveRange(existing.Insights);
            existing.NarrativeText = narrative;
            existing.GeneratedAtUtc = clock.UtcNow;
            existing.Insights = insights.Select(i => new GeneratedReportInsight
            {
                RuleCode = i.RuleCode,
                Severity = i.Severity,
                Message = i.Message,
            }).ToList();

            existing.DeliveryStatus = await deliveryDispatcher.DeliverAsync(existing.DeliveryChannel, athleteUserId, narrative, cancellationToken);
            existing.DeliveredAtUtc = existing.DeliveryStatus == ReportDeliveryStatus.Delivered ? clock.UtcNow : null;

            await db.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var report = new GeneratedReport
        {
            AthleteUserId = athleteUserId,
            Date = date,
            Type = type,
            GeneratedAtUtc = clock.UtcNow,
            NarrativeText = narrative,
            DeliveryChannel = ReportDeliveryChannel.InApp,
            Insights = insights.Select(i => new GeneratedReportInsight
            {
                RuleCode = i.RuleCode,
                Severity = i.Severity,
                Message = i.Message,
            }).ToList(),
        };

        report.DeliveryStatus = await deliveryDispatcher.DeliverAsync(report.DeliveryChannel, athleteUserId, narrative, cancellationToken);
        report.DeliveredAtUtc = report.DeliveryStatus == ReportDeliveryStatus.Delivered ? clock.UtcNow : null;

        db.GeneratedReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(report);
    }

    public async Task<GeneratedReportDto?> GetAsync(Guid athleteUserId, ReportType type, DateOnly date, CancellationToken cancellationToken = default)
    {
        await accessGuard.EnsureAthleteAccessAsync(athleteUserId, PermissionScope.ViewWellness, cancellationToken);

        var report = await db.GeneratedReports
            .Include(r => r.Insights)
            .FirstOrDefaultAsync(r => r.AthleteUserId == athleteUserId && r.Date == date && r.Type == type, cancellationToken);

        return report is null ? null : ToDto(report);
    }

    private static GeneratedReportDto ToDto(GeneratedReport report) => new(
        report.Id,
        report.AthleteUserId,
        report.Date,
        report.Type,
        report.GeneratedAtUtc,
        report.NarrativeText,
        report.DeliveryChannel,
        report.DeliveryStatus,
        report.Insights.Select(i => new GeneratedReportInsightDto(i.RuleCode, i.Severity, i.Message)).ToList());
}
