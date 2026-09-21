using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Wellness;

public class HrvMeasurement : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal RmssdMs { get; set; }

    /// <summary>SDNN-based HRV figure, reported alongside rMSSD by some sources (e.g. intervals.icu's <c>hrvSDNN</c>) — a different algorithm, not a duplicate of <see cref="RmssdMs"/>.</summary>
    public decimal? SdnnMs { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    public DateTime? MeasuredAtUtc { get; set; }
    public string? Notes { get; set; }

    /// <summary>What kind of reading this is (e.g. a single morning spot-check vs. a continuous
    /// overnight average) — descriptive only, not part of the dedup key. See
    /// <see cref="MeasurementContext"/>.</summary>
    public MeasurementContext Context { get; set; } = MeasurementContext.Unspecified;
}
