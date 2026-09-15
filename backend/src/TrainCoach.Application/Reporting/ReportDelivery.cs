using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Reporting;

/// <summary>
/// One implementation per delivery channel. Only <see cref="InAppReportDeliveryChannel"/> is
/// wired up for the MVP — persisting the GeneratedReport row already *is* in-app delivery, the
/// athlete reads it from the API. The others are real, registered contracts returning
/// NotConfigured, so adding a working channel later only means implementing the body, never
/// changing the pipeline. See docs/architecture.md.
/// </summary>
public interface IReportDeliveryChannel
{
    ReportDeliveryChannel Channel { get; }
    Task<ReportDeliveryStatus> DeliverAsync(Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default);
}

public class InAppReportDeliveryChannel : IReportDeliveryChannel
{
    public ReportDeliveryChannel Channel => ReportDeliveryChannel.InApp;

    public Task<ReportDeliveryStatus> DeliverAsync(Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default)
        => Task.FromResult(ReportDeliveryStatus.Delivered);
}

/// <summary>Activation: configure SMTP settings and an IEmailSender implementation, then swap this stub's body.</summary>
public class EmailReportDeliveryChannel : IReportDeliveryChannel
{
    public ReportDeliveryChannel Channel => ReportDeliveryChannel.Email;

    public Task<ReportDeliveryStatus> DeliverAsync(Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default)
        => Task.FromResult(ReportDeliveryStatus.NotConfigured);
}

/// <summary>Activation: register a web-push subscription per device and a VAPID key pair, then swap this stub's body.</summary>
public class PushReportDeliveryChannel : IReportDeliveryChannel
{
    public ReportDeliveryChannel Channel => ReportDeliveryChannel.Push;

    public Task<ReportDeliveryStatus> DeliverAsync(Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default)
        => Task.FromResult(ReportDeliveryStatus.NotConfigured);
}

/// <summary>Activation: register a Telegram bot, let the athlete link their chat id, then swap this stub's body.</summary>
public class TelegramReportDeliveryChannel : IReportDeliveryChannel
{
    public ReportDeliveryChannel Channel => ReportDeliveryChannel.Telegram;

    public Task<ReportDeliveryStatus> DeliverAsync(Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default)
        => Task.FromResult(ReportDeliveryStatus.NotConfigured);
}

public interface IReportDeliveryDispatcher
{
    Task<ReportDeliveryStatus> DeliverAsync(ReportDeliveryChannel channel, Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default);
}

public class ReportDeliveryDispatcher(IEnumerable<IReportDeliveryChannel> channels) : IReportDeliveryDispatcher
{
    public Task<ReportDeliveryStatus> DeliverAsync(ReportDeliveryChannel channel, Guid athleteUserId, string narrativeText, CancellationToken cancellationToken = default)
    {
        var handler = channels.FirstOrDefault(c => c.Channel == channel);
        return handler?.DeliverAsync(athleteUserId, narrativeText, cancellationToken)
            ?? Task.FromResult(ReportDeliveryStatus.NotConfigured);
    }
}
