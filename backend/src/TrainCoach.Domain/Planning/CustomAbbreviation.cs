using TrainCoach.Domain.Common;

namespace TrainCoach.Domain.Planning;

/// <summary>
/// User-managed dictionary of training shorthand (e.g. "WU" -> "warm-up / rozklus"). Deliberately
/// not hardcoded into application logic — it is pure reference data the coach maintains.
/// </summary>
public class CustomAbbreviation : AuditableEntity
{
    public Guid CoachUserId { get; set; }
    public string Abbreviation { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string? Description { get; set; }
}
