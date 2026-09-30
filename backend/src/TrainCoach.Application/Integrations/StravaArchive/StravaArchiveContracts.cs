using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations.StravaArchive;

public class StravaArchiveImportOptions
{
    public const string SectionName = "StravaArchiveImport";

    /// <summary>Where downloaded/uploaded archives wait for processing. Empty = a folder under the
    /// OS temp directory.</summary>
    public string? StoragePath { get; set; }

    /// <summary>Upper bound for one archive — Strava exports with years of history and photos can
    /// run to several GB.</summary>
    public long MaxArchiveBytes { get; set; } = 4L * 1024 * 1024 * 1024;

    /// <summary>Stored archives older than this are deleted by the startup/cleanup pass even if
    /// their import never finished (e.g. a preview nobody confirmed).</summary>
    public int RetentionHours { get; set; } = 48;

    public int DownloadTimeoutMinutes { get; set; } = 30;
}

/// <summary>Temporary on-disk home of archives between download/upload and import. Keys are
/// opaque generated names — never derived from user input.</summary>
public interface IStravaArchiveFileStore
{
    string CreateKey();
    Stream OpenWrite(string key);
    string GetPath(string key);
    void Delete(string key);

    /// <summary>Deletes stored archives last written before <paramref name="olderThanUtc"/>,
    /// except the <paramref name="keep"/> keys (archives of still-active imports).</summary>
    int DeleteOlderThan(DateTime olderThanUtc, IReadOnlySet<string> keep);
}

public record StravaArchiveDownloadResult(string? StravaAthleteId, long Bytes);

/// <summary>Fetches the archive behind an emailed link (see <see cref="StravaArchiveLink"/> for the
/// allow-list). Throws <see cref="TrainCoach.Application.Common.BusinessRuleException"/> with a
/// user-facing message for expired/invalid links. Never logs the URL — it's a bearer secret.</summary>
public interface IStravaArchiveDownloader
{
    Task<StravaArchiveDownloadResult> DownloadAsync(
        Uri url, Stream destination, long maxBytes, Func<long, long?, Task>? onProgress, CancellationToken cancellationToken = default);
}

public record CreateStravaArchiveImportFromLinkRequest(
    string Url,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<SportType>? Sports);

public record StravaArchiveImportDto(
    Guid Id,
    StravaArchiveImportStatus Status,
    StravaArchiveSourceKind SourceKind,
    string? OriginalFileName,
    long? SizeBytes,
    long DownloadedBytes,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<SportType> Sports,
    int? PreviewTotal,
    int? PreviewInFilter,
    int? PreviewAlreadyImported,
    int? PreviewWouldCreate,
    int? PreviewWouldMerge,
    int? PreviewWouldReview,
    int? PreviewStreamsToAdd,
    int ItemsProcessed,
    int ItemsCreated,
    int ItemsMerged,
    int ItemsFlaggedForReview,
    int ItemsSkippedDuplicate,
    int ItemsFailed,
    int ItemsStreamsAdded,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? PreviewReadyAtUtc,
    DateTime? FinishedAtUtc);

public interface IStravaArchiveImportService
{
    Task<StravaArchiveImportDto> CreateFromLinkAsync(Guid callerUserId, CreateStravaArchiveImportFromLinkRequest request, CancellationToken cancellationToken = default);

    Task<StravaArchiveImportDto> CreateFromUploadAsync(
        Guid callerUserId, string fileName, Stream content, DateOnly? fromDate, DateOnly? toDate, IReadOnlyList<SportType>? sports,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StravaArchiveImportDto>> ListAsync(Guid callerUserId, CancellationToken cancellationToken = default);
    Task<StravaArchiveImportDto> GetAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken = default);
    Task<StravaArchiveImportDto> ConfirmAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken = default);
    Task<StravaArchiveImportDto> CancelAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken = default);
}

/// <summary>The two background phases — run from <see cref="TrainCoach.Application.Common.IBackgroundJobQueue"/>.</summary>
public interface IStravaArchiveImportJob
{
    /// <summary>Download (link only) + dry-run analysis → PreviewReady. <paramref name="url"/> is
    /// passed in memory only, never persisted.</summary>
    Task AnalyzeAsync(Guid importId, Uri? url, CancellationToken cancellationToken = default);

    /// <summary>The confirmed import → Succeeded.</summary>
    Task ImportAsync(Guid importId, CancellationToken cancellationToken = default);

    /// <summary>Startup pass: marks imports a restart interrupted as failed, then <see cref="SweepExpiredAsync"/>.</summary>
    Task RecoverInterruptedAsync(CancellationToken cancellationToken = default);

    /// <summary>Periodic pass: expires previews nobody confirmed within the retention window and
    /// deletes orphaned archives. Never touches imports that are actively running.</summary>
    Task SweepExpiredAsync(CancellationToken cancellationToken = default);
}
