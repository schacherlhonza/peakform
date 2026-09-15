using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public interface IImportService
{
    /// <summary>Parses and validates the file but persists ONLY the raw upload — no domain
    /// records (activities etc.) are created until <see cref="ConfirmAsync"/> is called.</summary>
    Task<ImportPreviewDto> UploadAndPreviewAsync(Guid callerUserId, Guid athleteUserId, string fileName, string content, ImportFileType fileType, CancellationToken cancellationToken = default);

    Task<ImportConfirmResultDto> ConfirmAsync(Guid callerUserId, Guid importedFileId, CancellationToken cancellationToken = default);
}
