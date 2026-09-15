using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Integrations;

public interface ISyncOrchestrator
{
    Task RunAsync(Guid integrationConnectionId, SyncTrigger trigger, CancellationToken cancellationToken = default);
}
