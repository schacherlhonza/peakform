using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;
using TrainCoach.Domain.Enums;
using TrainCoach.Domain.Execution;
using TrainCoach.Domain.Integrations;

namespace TrainCoach.Application.Integrations;

public class ImportService(IApplicationDbContext db, IDateTimeProvider clock) : IImportService
{
    public async Task<ImportPreviewDto> UploadAndPreviewAsync(Guid callerUserId, Guid athleteUserId, string fileName, string content, ImportFileType fileType, CancellationToken cancellationToken = default)
    {
        if (callerUserId != athleteUserId)
        {
            // MVP: a coach could in principle import on an athlete's behalf, but keeping this to
            // "self only" avoids having to design consent for injecting historical data into
            // someone else's account — the athlete uploads their own export.
            throw new ForbiddenAccessException("Import lze provést pouze za vlastní účet.");
        }

        var rows = CsvImportParser.Parse(content);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

        var importedFile = new ImportedFile
        {
            UploadedByUserId = callerUserId,
            AthleteUserId = athleteUserId,
            FileType = fileType,
            OriginalFileName = fileName,
            RawContent = content,
            ContentHash = hash,
            Status = ImportStatus.PreviewReady,
            RowsTotal = rows.Count,
            RowsValid = rows.Count(r => r.Status == ImportRowStatus.Valid),
            RowsWithWarnings = rows.Count(r => r.Status == ImportRowStatus.Warning),
            RowsWithErrors = rows.Count(r => r.Status == ImportRowStatus.Error),
            PreviewGeneratedAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow,
        };
        db.ImportedFiles.Add(importedFile);
        await db.SaveChangesAsync(cancellationToken);

        return new ImportPreviewDto(
            importedFile.Id, fileName, importedFile.RowsTotal.Value, importedFile.RowsValid.Value,
            importedFile.RowsWithWarnings.Value, importedFile.RowsWithErrors.Value, rows);
    }

    public async Task<ImportConfirmResultDto> ConfirmAsync(Guid callerUserId, Guid importedFileId, CancellationToken cancellationToken = default)
    {
        var file = await db.ImportedFiles.FirstOrDefaultAsync(f => f.Id == importedFileId, cancellationToken)
            ?? throw new NotFoundException("ImportedFile", importedFileId);

        if (file.UploadedByUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Tento import nepatří vám.");
        }

        if (file.Status == ImportStatus.Confirmed)
        {
            throw new BusinessRuleException("Tento import už byl potvrzen.");
        }

        var rows = CsvImportParser.Parse(file.RawContent);

        var imported = 0;
        var skippedDuplicate = 0;
        var skippedInvalid = 0;

        foreach (var row in rows)
        {
            if (row.Status == ImportRowStatus.Error || row.Date is null || row.Sport is null)
            {
                skippedInvalid++;
                continue;
            }

            var externalId = $"{file.FileType}:{file.ContentHash}:{row.RowNumber}";
            var exists = await db.DataProvenances.AnyAsync(p => p.Source == DataSource.FileImport && p.ExternalId == externalId, cancellationToken);
            if (exists)
            {
                skippedDuplicate++;
                continue;
            }

            var activity = new CompletedActivity
            {
                AthleteUserId = file.AthleteUserId,
                Sport = row.Sport.Value,
                Title = row.Notes,
                StartedAtUtc = row.Date.Value.ToDateTime(TimeOnly.MinValue),
                DurationSeconds = (row.DurationMinutes ?? 0) * 60,
                DistanceMeters = row.DistanceKm * 1000,
                ElevationGainMeters = row.ElevationM,
                AverageHeartRateBpm = row.AverageHeartRate,
                CreatedAtUtc = clock.UtcNow,
                CreatedByUserId = callerUserId,
                Provenance = new DataProvenance
                {
                    Source = DataSource.FileImport,
                    ExternalId = externalId,
                    ImportedFileId = file.Id,
                    FetchedAtUtc = clock.UtcNow,
                },
            };
            db.CompletedActivities.Add(activity);
            imported++;
        }

        file.Status = ImportStatus.Confirmed;
        file.ConfirmedAtUtc = clock.UtcNow;
        file.RowsSkippedDuplicate = skippedDuplicate;

        await db.SaveChangesAsync(cancellationToken);

        return new ImportConfirmResultDto(file.Id, imported, skippedDuplicate, skippedInvalid);
    }
}
