using BlueBirdsERP.Application.Abstractions;

namespace BlueBirdsERP.Infrastructure.Notifications;

public sealed class TwilioWhatsAppNotificationService : INotificationService
{
    public Task QueueAsync(NotificationEnvelope notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

