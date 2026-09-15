using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public record ImportPreviewRowDto(
    int RowNumber,
    DateOnly? Date,
    SportType? Sport,
    decimal? DistanceKm,
    int? DurationMinutes,
    decimal? ElevationM,
    int? AverageHeartRate,
    string? Notes,
    ImportRowStatus Status,
    IReadOnlyList<string> Messages);

public record ImportPreviewDto(
    Guid ImportedFileId,
    string OriginalFileName,
    int RowsTotal,
    int RowsValid,
    int RowsWithWarnings,
    int RowsWithErrors,
    IReadOnlyList<ImportPreviewRowDto> Rows);

public record ImportConfirmResultDto(Guid ImportedFileId, int RowsImported, int RowsSkippedDuplicate, int RowsSkippedInvalid);
