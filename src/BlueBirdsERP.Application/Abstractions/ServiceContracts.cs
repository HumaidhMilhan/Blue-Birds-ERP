using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Abstractions;

public interface IAuthenticationService
{
    Task<AuthenticatedUser?> SignInAsync(string username, string password, CancellationToken cancellationToken = default);
}

public interface ISessionService
{
    TimeSpan InactivityTimeout { get; }
    Task BeginSessionAsync(AuthenticatedUser user, CancellationToken cancellationToken = default);
    Task TouchAsync(CancellationToken cancellationToken = default);
    Task EndSessionAsync(CancellationToken cancellationToken = default);
}

public interface IPOSCheckoutService
{
    Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
}

public interface IInventoryService
{
    Task<IReadOnlyList<Batch>> GetAvailableBatchesAsync(Guid productId, CancellationToken cancellationToken = default);
    Task DeductBatchStockAsync(Guid batchId, decimal quantity, CancellationToken cancellationToken = default);
    Task<WastageRecord> RecordWastageAsync(WastageRecord wastageRecord, CancellationToken cancellationToken = default);
}

public interface ICustomerCreditService
{
    Task<CreditSummary> GetCreditSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task ApplyAccountCreditAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<Payment> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task QueueAsync(NotificationEnvelope notification, CancellationToken cancellationToken = default);
    Task RetryFailedAsync(CancellationToken cancellationToken = default);
}

public interface IAuditLogger
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

public interface IOfflineSyncQueue
{
    Task EnqueueAsync(OfflineSyncEnvelope envelope, CancellationToken cancellationToken = default);
    Task<int> FlushAsync(CancellationToken cancellationToken = default);
}

public interface IReceiptPrinter
{
    Task PrintInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedUser(
    Guid UserId,
    string Username,
    UserRole Role);

public sealed record CheckoutRequest(
    SaleChannel SaleChannel,
    Guid? CustomerId,
    Guid CashierId,
    PaymentMethod PaymentMethod,
    IReadOnlyCollection<CheckoutLineItem> Lines,
    decimal CashAmount,
    decimal CardAmount,
    decimal CreditAmount);

public sealed record CheckoutLineItem(
    Guid ProductId,
    Guid BatchId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount);

public sealed record CheckoutResult(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal BalanceAmount,
    PaymentStatus PaymentStatus);

public sealed record CreditSummary(
    Guid CustomerId,
    decimal OutstandingBalance,
    decimal CreditLimit,
    decimal AvailableCredit,
    int OverdueInvoiceCount,
    DateTimeOffset? LastPaymentDate,
    bool IsLimitAlertActive,
    bool IsLimitBlocking);

public sealed record RecordPaymentRequest(
    Guid InvoiceId,
    Guid CustomerId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Reference,
    Guid RecordedBy);

public sealed record NotificationEnvelope(
    NotificationType NotificationType,
    Guid? CustomerId,
    Guid? InvoiceId,
    string MessageBody,
    DateTimeOffset ScheduledAt);

public sealed record AuditEntry(
    Guid UserId,
    UserRole Role,
    string Action,
    string Module,
    string TargetEntity,
    Guid TargetId,
    string? BeforeValueJson,
    string? AfterValueJson);

public sealed record OfflineSyncEnvelope(
    string EntityName,
    Guid EntityId,
    string Operation,
    string PayloadJson,
    DateTimeOffset QueuedAt);

