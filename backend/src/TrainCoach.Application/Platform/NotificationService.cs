using Microsoft.EntityFrameworkCore;
using TrainCoach.Application.Common;

namespace TrainCoach.Application.Platform;

/// <summary>
/// Notifications are purely "my own inbox" data — not athlete/coach relationship-scoped like
/// the rest of the app, so no IRelationshipAccessGuard is involved, just the caller's own id.
/// </summary>
public class NotificationService(IApplicationDbContext db, IDateTimeProvider clock) : INotificationService
{
    private const int MaxResults = 50;

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(MaxResults)
            .ToListAsync(cancellationToken);

        return notifications.Select(ToDto).ToList();
    }

    public async Task MarkReadAsync(Guid callerUserId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken)
            ?? throw new NotFoundException("Notification", notificationId);

        if (notification.RecipientUserId != callerUserId)
        {
            throw new ForbiddenAccessException("Notifikaci lze označit za přečtenou pouze pro sebe.");
        }

        notification.ReadAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationDto ToDto(Domain.Platform.Notification n) => new(
        n.Id, n.RecipientUserId, n.Type, n.Title, n.Body, n.LinkUrl, n.CreatedAtUtc, n.ReadAtUtc);
}
