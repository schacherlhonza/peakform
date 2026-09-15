using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Platform;

public record NotificationDto(
    Guid Id,
    Guid RecipientUserId,
    NotificationType Type,
    string Title,
    string? Body,
    string? LinkUrl,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
