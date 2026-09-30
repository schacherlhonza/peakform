using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Integrations;

namespace TrainCoach.Application.Integrations.StravaArchive;

public class StravaArchiveImportService(
    IApplicationDbContext db,
    IStravaArchiveFileStore fileStore,
    IBackgroundJobQueue jobQueue,
    IOptions<StravaArchiveImportOptions> options,
    IDateTimeProvider clock) : IStravaArchiveImportService
{
    internal static readonly StravaArchiveImportStatus[] ActiveStatuses =
    [
        StravaArchiveImportStatus.Pending, StravaArchiveImportStatus.Downloading, StravaArchiveImportStatus.Analyzing,
        StravaArchiveImportStatus.PreviewReady, StravaArchiveImportStatus.Importing,
    ];

    public async Task<StravaArchiveImportDto> CreateFromLinkAsync(Guid callerUserId, CreateStravaArchiveImportFromLinkRequest request, CancellationToken cancellationToken = default)
    {
        if (!StravaArchiveLink.TryParse(request.Url, out var url))
        {
            throw new BusinessRuleException("Odkaz není odkazem na archiv ze Stravy. Vložte odkaz z tlačítka „Download Archive“ v e-mailu od Stravy.");
        }

        var import = await CreateAsync(callerUserId, StravaArchiveSourceKind.Link, fileName: null, request.FromDate, request.ToDate, request.Sports, cancellationToken);
        import.StravaAthleteId = StravaArchiveLink.AthleteIdFromArchiveUrl(url);
        await db.SaveChangesAsync(cancellationToken);

        await jobQueue.QueueStravaArchiveAnalysisAsync(import.Id, url, cancellationToken);
        return ToDto(import);
    }

    public async Task<StravaArchiveImportDto> CreateFromUploadAsync(
        Guid callerUserId, string fileName, Stream content, DateOnly? fromDate, DateOnly? toDate, IReadOnlyList<SportType>? sports,
        CancellationToken cancellationToken = default)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Nahrajte ZIP archiv exportu ze Stravy.");
        }

        var import = await CreateAsync(callerUserId, StravaArchiveSourceKind.Upload, Path.GetFileName(fileName), fromDate, toDate, sports, cancellationToken);
        import.StravaAthleteId = StravaArchiveLink.AthleteIdFromFileName(fileName);
        import.StorageKey = fileStore.CreateKey();

        try
        {
            await using (var destination = fileStore.OpenWrite(import.StorageKey))
            {
                await CopyWithLimitAsync(content, destination, options.Value.MaxArchiveBytes, cancellationToken);
            }
            import.SizeBytes = import.DownloadedBytes = new FileInfo(fileStore.GetPath(import.StorageKey)).Length;
        }
        catch
        {
            fileStore.Delete(import.StorageKey);
            db.StravaArchiveImports.Remove(import);
            throw;
        }

        await db.SaveChangesAsync(cancellationToken);
        await jobQueue.QueueStravaArchiveAnalysisAsync(import.Id, url: null, cancellationToken);
        return ToDto(import);
    }

    public async Task<IReadOnlyList<StravaArchiveImportDto>> ListAsync(Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var imports = await db.StravaArchiveImports.AsNoTracking()
            .Where(i => i.AthleteUserId == callerUserId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return imports.Select(ToDto).ToList();
    }

    public async Task<StravaArchiveImportDto> GetAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken = default) =>
        ToDto(await LoadOwnAsync(callerUserId, importId, cancellationToken));

    public async Task<StravaArchiveImportDto> ConfirmAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken = default)
    {
        var import = await LoadOwnAsync(callerUserId, importId, cancellationToken);
        if (import.Status != StravaArchiveImportStatus.PreviewReady)
        {
            throw new BusinessRuleException("Import lze potvrdit jen ve chvíli, kdy je připravený náhled.");
        }

        import.Status = StravaArchiveImportStatus.Importing;
        import.ItemsProcessed = 0;
        import.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await jobQueue.QueueStravaArchiveImportAsync(import.Id, cancellationToken);
        return ToDto(import);
    }

    public async Task<StravaArchiveImportDto> CancelAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken = default)
    {
        var import = await LoadOwnAsync(callerUserId, importId, cancellationToken);
        // Importing is deliberately not cancellable: it's idempotent and partial results are
        // already real activities — stopping halfway would only leave a confusing half-import.
        if (import.Status is not (StravaArchiveImportStatus.Pending or StravaArchiveImportStatus.Downloading
            or StravaArchiveImportStatus.Analyzing or StravaArchiveImportStatus.PreviewReady))
        {
            throw new BusinessRuleException("Tento import už nelze zrušit.");
        }

        // A running download/analysis notices the status change at its next progress save and
        // stops (see StravaArchiveImportJob); the file is deleted here or by the job, whichever
        // is last.
        import.Status = StravaArchiveImportStatus.Cancelled;
        import.FinishedAtUtc = clock.UtcNow;
        import.UpdatedAtUtc = clock.UtcNow;
        if (import.StorageKey is not null)
        {
            fileStore.Delete(import.StorageKey);
        }
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(import);
    }

    private async Task<StravaArchiveImport> CreateAsync(
        Guid callerUserId, StravaArchiveSourceKind kind, string? fileName, DateOnly? fromDate, DateOnly? toDate, IReadOnlyList<SportType>? sports,
        CancellationToken cancellationToken)
    {
        if (fromDate is not null && toDate is not null && fromDate > toDate)
        {
            throw new BusinessRuleException("Počáteční datum musí být před datem ukončení.");
        }

        var hasActive = await db.StravaArchiveImports.AnyAsync(
            i => i.AthleteUserId == callerUserId && ActiveStatuses.Contains(i.Status), cancellationToken);
        if (hasActive)
        {
            throw new BusinessRuleException("Už máte rozpracovaný import ze Stravy. Dokončete ho nebo zrušte, než začnete nový.");
        }

        // Self-only, like ImportService: an athlete imports their own export, never a coach on
        // their behalf (no consent model for injecting history into someone else's account).
        var import = new StravaArchiveImport
        {
            AthleteUserId = callerUserId,
            CreatedByUserId = callerUserId,
            SourceKind = kind,
            OriginalFileName = fileName,
            FromDate = fromDate,
            ToDate = toDate,
            SportsFilter = sports is { Count: > 0 } ? string.Join(',', sports.Distinct().Select(s => (int)s)) : null,
            CreatedAtUtc = clock.UtcNow,
        };
        db.StravaArchiveImports.Add(import);
        return import;
    }

    private async Task<StravaArchiveImport> LoadOwnAsync(Guid callerUserId, Guid importId, CancellationToken cancellationToken)
    {
        var import = await db.StravaArchiveImports.FirstOrDefaultAsync(i => i.Id == importId, cancellationToken);
        // 404 rather than 403 for someone else's import — don't confirm it exists.
        if (import is null || import.AthleteUserId != callerUserId)
        {
            throw new NotFoundException("StravaArchiveImport", importId);
        }
        return import;
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new BusinessRuleException($"Archiv je větší než povolený limit {maxBytes / (1024 * 1024)} MB.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    internal static IReadOnlyList<SportType> ParseSports(string? filter) =>
        string.IsNullOrWhiteSpace(filter)
            ? []
            : filter.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => (SportType)int.Parse(v)).ToList();

    internal static StravaArchiveImportDto ToDto(StravaArchiveImport i) => new(
        i.Id, i.Status, i.SourceKind, i.OriginalFileName, i.SizeBytes, i.DownloadedBytes, i.FromDate, i.ToDate, ParseSports(i.SportsFilter),
        i.PreviewTotal, i.PreviewInFilter, i.PreviewAlreadyImported, i.PreviewWouldCreate, i.PreviewWouldMerge, i.PreviewWouldReview,
        i.PreviewStreamsToAdd,
        i.ItemsProcessed, i.ItemsCreated, i.ItemsMerged, i.ItemsFlaggedForReview, i.ItemsSkippedDuplicate, i.ItemsFailed,
        i.ItemsStreamsAdded,
        i.ErrorMessage, i.CreatedAtUtc, i.StartedAtUtc, i.PreviewReadyAtUtc, i.FinishedAtUtc);
}
