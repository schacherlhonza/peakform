using TrainCoach.Domain.Common;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Domain.Integrations;

/// <summary>
/// An uploaded file for the import flow. <see cref="RawContent"/> is the only thing persisted
/// at upload time — the preview (parsed rows, validation, ambiguity flags) is generated
/// on-demand from it and returned to the client without being saved; real domain records are
/// only created once the user explicitly confirms (see docs/architecture.md, import flow).
/// </summary>
public class ImportedFile : AuditableEntity
{
    public Guid UploadedByUserId { get; set; }
    public Guid AthleteUserId { get; set; }
    public ImportFileType FileType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;

    public ImportStatus Status { get; set; } = ImportStatus.Uploaded;
    public int? RowsTotal { get; set; }
    public int? RowsValid { get; set; }
    public int? RowsWithWarnings { get; set; }
    public int? RowsWithErrors { get; set; }
    public int? RowsSkippedDuplicate { get; set; }

    public DateTime? PreviewGeneratedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
}
