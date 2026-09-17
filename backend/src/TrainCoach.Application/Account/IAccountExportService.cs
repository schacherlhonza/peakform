namespace TrainCoach.Application.Account;

public interface IAccountExportService
{
    Task<AccountDataExportDto> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}
