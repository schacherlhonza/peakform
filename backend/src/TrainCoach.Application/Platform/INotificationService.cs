namespace TrainCoach.Application.Platform;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid recipientUserId, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid callerUserId, Guid notificationId, CancellationToken cancellationToken = default);
}
