using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Integrations;

/// <summary>
/// One bulk import of a Strava "Download your data" archive (the GDPR export ZIP). Runs in two
/// background phases: download/analyze into a dry-run preview (<see cref="StravaArchiveImportStatus.PreviewReady"/>),
/// then — only after the athlete confirms — the actual import through the same ingestion pipeline
/// as the live sync. The emailed download link is a bearer secret (7-day presigned S3 URL) and is
/// deliberately never persisted; only the downloaded ZIP is, under <see cref="StorageKey"/>, and
/// only until the import finishes. See docs/integrations/strava-archive-import.md.
/// </summary>
public class StravaArchiveImport : AuditableEntity
{
    public Guid AthleteUserId { get; set; }
    public StravaArchiveImportStatus Status { get; set; } = StravaArchiveImportStatus.Pending;
    public StravaArchiveSourceKind SourceKind { get; set; }
    public string? OriginalFileName { get; set; }
    public long? SizeBytes { get; set; }
    public long DownloadedBytes { get; set; }

    /// <summary>Strava athlete id read from the S3 path or the <c>export_{id}.zip</c> file name —
    /// checked against the connected Strava account so someone else's archive can't be imported.</summary>
    public string? StravaAthleteId { get; set; }

    /// <summary>Opaque file name inside the temp archive store. Internal only, never exposed via DTO.</summary>
    public string? StorageKey { get; set; }

    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    /// <summary>Comma-separated <see cref="SportType"/> values; null/empty = all sports.</summary>
    public string? SportsFilter { get; set; }

    public int? PreviewTotal { get; set; }
    public int? PreviewInFilter { get; set; }
    public int? PreviewAlreadyImported { get; set; }
    public int? PreviewWouldCreate { get; set; }
    public int? PreviewWouldMerge { get; set; }
    public int? PreviewWouldReview { get; set; }

    /// <summary>Activities (new or already present) that will get a stored detail stream — an
    /// estimate: counts rows with an activity file whose record has no stream yet.</summary>
    public int? PreviewStreamsToAdd { get; set; }

    public int ItemsProcessed { get; set; }
    public int ItemsCreated { get; set; }
    public int ItemsMerged { get; set; }
    public int ItemsFlaggedForReview { get; set; }
    public int ItemsSkippedDuplicate { get; set; }
    public int ItemsFailed { get; set; }
    public int ItemsStreamsAdded { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? PreviewReadyAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
}
