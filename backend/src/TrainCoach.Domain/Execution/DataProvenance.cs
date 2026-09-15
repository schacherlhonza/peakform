using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// Records where a <see cref="CompletedActivity"/> came from and carries its dedup key
/// (Source + ExternalId, falling back to time+distance fuzzy matching when a provider gives no
/// stable external id). Raw provider payloads are only kept when <see cref="RawPayloadRetained"/>
/// is true — i.e. when that provider's terms allow storing it (see docs/security.md).
/// </summary>
public class DataProvenance : Entity
{
    public Guid CompletedActivityId { get; set; }
    public CompletedActivity CompletedActivity { get; set; } = null!;

    public DataSource Source { get; set; }
    public string? ExternalId { get; set; }
    public Guid? SynchronizationRunId { get; set; }
    public Guid? ImportedFileId { get; set; }

    public bool RawPayloadRetained { get; set; }
    public string? RawPayloadJson { get; set; }

    public DateTime FetchedAtUtc { get; set; }
}
