using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Execution;

/// <summary>
/// One provider's record of a <see cref="CompletedActivity"/> — the dedup/matching unit. Several
/// of these can point at the same canonical activity (e.g. Strava direct + intervals.icu both
/// reporting the same real-world run); exactly one is the activity's
/// <see cref="CompletedActivity.PrimarySourceRecordId"/>. Raw provider payloads are only kept when
/// <see cref="RawPayloadRetained"/> is true — i.e. when that provider's terms allow storing it (see
/// docs/security.md). Was named <c>DataProvenance</c> and was 1:1 with <see cref="CompletedActivity"/>
/// before the canonical-data/deduplication rework — see
/// docs/integrations/canonical-data-and-deduplication-plan.md.
/// </summary>
public class ActivitySourceRecord : Entity
{
    public Guid CompletedActivityId { get; set; }
    public CompletedActivity CompletedActivity { get; set; } = null!;

    public DataSource Source { get; set; }
    public string? ExternalId { get; set; }
    public Guid? SynchronizationRunId { get; set; }
    public Guid? ImportedFileId { get; set; }

    /// <summary>Set when this record came from a Strava archive import rather than the live API —
    /// the Source is still <see cref="DataSource.Strava"/> (same ExternalId space, so level-1 dedup
    /// works both ways), but archive data is the athlete's own export, not API data.</summary>
    public Guid? StravaArchiveImportId { get; set; }

    public bool RawPayloadRetained { get; set; }
    public string? RawPayloadJson { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    /// <summary>E.g. "Garmin Forerunner 965" — the device the source provider says recorded this
    /// activity, when it reports one (today only intervals.icu's raw payload carries this, and it
    /// used to be discarded; now persisted structurally for the fingerprint/matcher's device-match
    /// signal). Null when the source doesn't report a device.</summary>
    public string? DeviceName { get; set; }

    /// <summary>FIT file session identity (uuid/hash of file+session timestamp+device serial) when
    /// available — level-3 match key, stronger than the fingerprint. Populated by the Strava archive
    /// import (from the FIT file_id message); neither live adapter reports it today.</summary>
    public string? FitFileUuid { get; set; }

    public decimal? RawStartLatitude { get; set; }
    public decimal? RawStartLongitude { get; set; }

    /// <summary>This source record's own candidate-index fingerprint — see
    /// <see cref="CompletedActivity.NormalizedFingerprint"/> for the canonical-activity-level cache
    /// of the same concept, and IActivityMatchingService for how it's computed/used.</summary>
    public string? NormalizedFingerprint { get; set; }

    /// <summary>Stored detail stream, when this source's data may be retained (Strava archive,
    /// intervals.icu device file).</summary>
    public ActivityStream? Stream { get; set; }

    /// <summary>When the stream backfill last tried to download this record's activity file —
    /// set whether or not it yielded a stream, so files that can't be parsed (or activities
    /// without one) aren't re-downloaded on every sync.</summary>
    public DateTime? StreamFetchAttemptedAtUtc { get; set; }
}
