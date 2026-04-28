using BlueBirdsERP.Application.Abstractions;

namespace BlueBirdsERP.Infrastructure.Notifications;

public sealed class TwilioWhatsAppNotificationService : IWhatsAppNotificationQueue
{
    public Task EnqueueAsync(QueuedWhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
