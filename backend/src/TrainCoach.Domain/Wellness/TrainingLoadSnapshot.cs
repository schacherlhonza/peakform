using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

/// <summary>
/// Daily Performance Management Chart figures (CTL/ATL/ramp rate) as computed by a provider (e.g.
/// intervals.icu) from the athlete's training stress — unrelated to <see cref="PerformanceBaseline"/>,
/// which is TrainCoach's own rolling mean/std-deviation of a handful of other metrics. Read-only
/// by design: nobody manually knows their own CTL/ATL, so there's no manual-entry endpoint for this.
/// </summary>
public class TrainingLoadSnapshot : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal? Ctl { get; set; }
    public decimal? Atl { get; set; }
    public decimal? RampRate { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
}
