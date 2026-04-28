using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Notifications;

public interface INotificationDataStore
{
    Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task UpdateNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetNotificationLogAsync(
        Guid? customerId,
        Guid? invoiceId,
        NotificationType? notificationType,
        CancellationToken cancellationToken = default);
    Task<bool> HasNotificationAsync(
        Guid? customerId,
        Guid? invoiceId,
        NotificationType notificationType,
        DateOnly? scheduledDate = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetFailedNotificationsDueForRetryAsync(
        DateTimeOffset asOf,
        int maxRetryCount,
        CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetNotificationTemplateAsync(NotificationType notificationType, CancellationToken cancellationToken = default);
    Task UpsertNotificationTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetOutstandingCreditInvoicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetInvoicesForDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WastageRecord>> GetWastageRecordsForDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public sealed class NotificationValidationException(string message) : InvalidOperationException(message);
