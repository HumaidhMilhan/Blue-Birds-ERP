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
    Task<bool> IsSessionActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task InvalidateUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IRbacAuthorizationService
{
    bool HasPermission(UserRole role, RbacPermission permission);
    IReadOnlySet<RbacPermission> GetPermissions(UserRole role);
}

public interface IUserManagementService
{
    Task<CashierAccountResult> CreateCashierAsync(CreateCashierRequest request, CancellationToken cancellationToken = default);
    Task<CashierAccountResult> DeactivateCashierAsync(DeactivateCashierRequest request, CancellationToken cancellationToken = default);
    Task<PasswordResetResult> ResetCashierPasswordAsync(ResetCashierPasswordRequest request, CancellationToken cancellationToken = default);
}

public interface ILoginSessionFacade
{
    AuthenticatedUser? CurrentUser { get; }
    IReadOnlySet<RbacPermission> CurrentPermissions { get; }
    Task<LoginResult> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public interface IRecoveryAccessService
{
    Task<RecoveryAccessResult> GenerateTemporaryAdminPasswordAsync(RecoveryAccessRequest request, CancellationToken cancellationToken = default);
}

public interface IApplicationBootstrapService
{
    Task<BootstrapResult> EnsureDevelopmentBootstrapAsync(CancellationToken cancellationToken = default);
}

public interface IPOSCheckoutService
{
    IReadOnlyList<SaleChannel> GetSaleChannels();
    IReadOnlyList<PaymentMethod> GetAllowedPaymentMethods(SaleChannel saleChannel);
    Task<IReadOnlyList<BatchPickerOption>> GetBatchPickerOptionsAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<CreditSummary?> GetCreditPanelAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<VoidInvoiceResult> VoidInvoiceAsync(VoidInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<SalesReturnResult> ProcessSalesReturnAsync(SalesReturnRequest request, CancellationToken cancellationToken = default);
}

public interface IInventoryService
{
    Task<ProductCategoryResult> CreateProductCategoryAsync(CreateProductCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ProductCatalogItem> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<BatchResult> RecordManualBatchPurchaseAsync(ManualBatchPurchaseRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> GetAvailableBatchesAsync(Guid productId, CancellationToken cancellationToken = default);
    Task DeductBatchStockAsync(Guid batchId, decimal quantity, CancellationToken cancellationToken = default);
    Task<WastageRecord> RecordWastageAsync(RecordWastageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductStockLevel>> GetProductStockLevelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryAlert>> GetInventoryAlertsAsync(DateOnly asOfDate, int nearExpiryThresholdDays, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatchHistoryEntry>> GetBatchHistoryAsync(Guid productId, CancellationToken cancellationToken = default);
}

public interface ICustomerCreditService
{
    Task<CreditSummary> GetCreditSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task ApplyAccountCreditAsync(Guid customerId, decimal amount, CancellationToken cancellationToken = default);
}

public interface ICustomerAccountService
{
    Task<CustomerAccountResult> CreateBusinessAccountAsync(CreateBusinessAccountRequest request, CancellationToken cancellationToken = default);
    Task<CustomerAccountResult> CreateOneTimeCreditorAsync(CreateOneTimeCreditorRequest request, CancellationToken cancellationToken = default);
    Task<CreditSummary> GetCreditSummaryAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<AccountPaymentResult> RecordAccountPaymentAsync(AccountPaymentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerPaymentHistoryEntry>> GetPaymentHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerAccountResult> UpdateBusinessAccountTermsAsync(UpdateBusinessAccountTermsRequest request, CancellationToken cancellationToken = default);
    Task<DebtorAgingReport> GenerateDebtorAgingReportAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<Payment> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task QueueAsync(NotificationEnvelope notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationResult>> QueuePaymentRemindersAsync(PaymentReminderRunRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationResult>> QueueOverdueRemindersAsync(NotificationRunRequest request, CancellationToken cancellationToken = default);
    Task<OwnerDailySummaryResult> QueueOwnerDailySummaryAsync(OwnerDailySummaryRequest request, CancellationToken cancellationToken = default);
    Task<NotificationTemplateResult> UpdateTemplateAsync(UpdateNotificationTemplateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationLogEntry>> GetNotificationLogAsync(NotificationLogQuery query, CancellationToken cancellationToken = default);
    Task<int> RetryFailedAsync(RetryFailedNotificationsRequest request, CancellationToken cancellationToken = default);
}

public interface IWhatsAppNotificationQueue
{
    Task EnqueueAsync(QueuedWhatsAppMessage message, CancellationToken cancellationToken = default);
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

public interface ISystemSettingsService
{
    Task<IReadOnlyList<SystemSettingResult>> GetSettingsAsync(SystemSettingsQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemSettingResult>> UpdateSettingsAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken = default);
}

public interface IDatabaseManagementService
{
    Task<DatabaseOperationResult> TestSqliteConnectionAsync(AdminOperationRequest request, CancellationToken cancellationToken = default);
    Task<DatabaseOperationResult> TestPostgreSqlConnectionAsync(AdminOperationRequest request, CancellationToken cancellationToken = default);
    Task<DatabaseOperationResult> ApplyMigrationsAsync(AdminOperationRequest request, CancellationToken cancellationToken = default);
    Task<DatabaseBackupResult> BackupSqliteDatabaseAsync(DatabaseBackupRequest request, CancellationToken cancellationToken = default);
    Task<OfflineSyncQueueStatusResult> GetSyncQueueStatusAsync(AdminOperationRequest request, CancellationToken cancellationToken = default);
}

public interface IReceiptPdfService
{
    Task<ReceiptPdfResult> GenerateInvoiceReceiptPdfAsync(ReceiptPdfRequest request, CancellationToken cancellationToken = default);
}

public interface IReportingService
{
    Task<OperationalReportResult> GenerateOperationalReportAsync(OperationalReportRequest request, CancellationToken cancellationToken = default);
}

public interface IAuditLogReader
{
    Task<IReadOnlyList<AuditLogEntryResult>> QueryAsync(AuditLogQuery request, CancellationToken cancellationToken = default);
}

public sealed record AuthenticatedUser(
    Guid UserId,
    string Username,
    UserRole Role);

public sealed record CreateCashierRequest(
    string Username,
    Guid AdminUserId,
    UserRole AdminRole);

public sealed record DeactivateCashierRequest(
    Guid CashierUserId,
    Guid AdminUserId,
    UserRole AdminRole);

public sealed record ResetCashierPasswordRequest(
    Guid CashierUserId,
    Guid AdminUserId,
    UserRole AdminRole);

public sealed record CashierAccountResult(
    Guid UserId,
    string Username,
    bool IsActive,
    string? TemporaryPassword = null);

public sealed record PasswordResetResult(
    Guid UserId,
    string Username,
    string TemporaryPassword,
    DateTimeOffset PasswordChangedAt);

public sealed record LoginRequest(
    string Username,
    string Password);

public sealed record LoginResult(
    bool IsAuthenticated,
    AuthenticatedUser? User,
    IReadOnlySet<RbacPermission> Permissions,
    string? ErrorMessage);

public sealed record RecoveryAccessRequest(
    string RecoveryKey,
    string? TargetAdminUsername = null);

public sealed record RecoveryAccessResult(
    Guid AdminUserId,
    string Username,
    string TemporaryPassword,
    DateTimeOffset PasswordChangedAt);

public sealed record BootstrapResult(
    bool Created,
    string Username);

public sealed record CheckoutRequest(
    SaleChannel SaleChannel,
    Guid? CustomerId,
    Guid CashierId,
    PaymentMethod PaymentMethod,
    IReadOnlyCollection<CheckoutLineItem> Lines,
    decimal CashAmount,
    decimal CardAmount,
    decimal CreditAmount,
    DateTime? ManualDueDate,
    string? Notes = null);

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
    PaymentStatus PaymentStatus,
    DateTime? DueDate,
    InvoiceReceipt Receipt,
    CreditSummary? CreditPanel);

public sealed record CreditSummary(
    Guid CustomerId,
    decimal OutstandingBalance,
    decimal? CreditLimit,
    decimal? AvailableCredit,
    int OverdueInvoiceCount,
    DateTimeOffset? LastPaymentDate,
    bool IsLimitAlertActive,
    bool IsLimitBlocking);

public sealed record CreateBusinessAccountRequest(
    string Name,
    string Phone,
    string WhatsAppNo,
    string? Email,
    string? Address,
    decimal CreditLimit,
    int CreditPeriodDays,
    int NotificationLeadDays,
    Guid CreatedBy,
    UserRole Role);

public sealed record CreateOneTimeCreditorRequest(
    string FullName,
    string Phone,
    string WhatsAppNo,
    string? Address,
    string? NicOrBusinessRegistrationNumber,
    Guid CreatedBy,
    UserRole Role);

public sealed record CustomerAccountResult(
    Guid CustomerId,
    Guid? AccountId,
    string Name,
    AccountType AccountType,
    decimal OutstandingBalance,
    decimal? CreditLimit,
    decimal? AvailableCredit);

public sealed record AccountPaymentRequest(
    Guid CustomerId,
    Guid? InvoiceId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Reference,
    Guid RecordedBy,
    UserRole Role);

public sealed record AccountPaymentResult(
    Guid CustomerId,
    decimal AmountApplied,
    decimal RemainingOutstanding,
    IReadOnlyList<InvoicePaymentAllocation> Allocations);

public sealed record InvoicePaymentAllocation(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal AmountApplied,
    decimal RemainingInvoiceBalance,
    PaymentStatus PaymentStatus);

public sealed record CustomerPaymentHistoryEntry(
    string InvoiceNumber,
    DateTimeOffset Date,
    decimal Amount,
    PaymentStatus Status);

public sealed record UpdateBusinessAccountTermsRequest(
    Guid CustomerId,
    decimal CreditLimit,
    int CreditPeriodDays,
    int NotificationLeadDays,
    Guid UpdatedBy,
    UserRole Role);

public sealed record DebtorAgingReport(
    DateOnly AsOfDate,
    IReadOnlyList<DebtorAgingBucket> Buckets);

public sealed record DebtorAgingBucket(
    string Name,
    decimal OutstandingBalance,
    IReadOnlyList<DebtorAgingInvoice> Invoices);

public sealed record DebtorAgingInvoice(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    DateTimeOffset InvoiceDate,
    DateTime? DueDate,
    decimal BalanceAmount);

public sealed record CreateProductCategoryRequest(
    string Name,
    Guid CreatedBy,
    UserRole Role);

public sealed record ProductCategoryResult(
    Guid CategoryId,
    string Name,
    bool IsActive);

public sealed record CreateProductRequest(
    Guid CategoryId,
    string Name,
    PricingType PricingType,
    string UnitOfMeasure,
    decimal SellingPrice,
    decimal ReorderLevel,
    Guid CreatedBy,
    UserRole Role);

public sealed record ProductCatalogItem(
    Guid ProductId,
    Guid CategoryId,
    string Name,
    PricingType PricingType,
    string UnitOfMeasure,
    decimal SellingPrice,
    decimal ReorderLevel,
    bool IsActive);

public sealed record ManualBatchPurchaseRequest(
    Guid ProductId,
    DateTime PurchaseDate,
    DateTime? ExpiryDate,
    decimal InitialQuantity,
    decimal CostPrice,
    Guid RecordedBy,
    UserRole Role);

public sealed record RecordWastageRequest(
    Guid BatchId,
    DateTime WastageDate,
    decimal Quantity,
    WastageType WastageType,
    string? Notes,
    Guid RecordedBy,
    UserRole Role);

public sealed record BatchResult(
    Guid BatchId,
    Guid ProductId,
    DateTime PurchaseDate,
    DateTime? ExpiryDate,
    decimal InitialQuantity,
    decimal RemainingQuantity,
    decimal CostPrice,
    BatchStatus Status);

public sealed record ProductStockLevel(
    Guid ProductId,
    string ProductName,
    string UnitOfMeasure,
    decimal RemainingQuantity,
    decimal ReorderLevel);

public sealed record InventoryAlert(
    string AlertType,
    Guid ProductId,
    string ProductName,
    Guid? BatchId,
    string Message);

public sealed record BatchHistoryEntry(
    Guid BatchId,
    Guid ProductId,
    string ProductName,
    DateTime PurchaseDate,
    DateTime? ExpiryDate,
    decimal PurchasedQuantity,
    decimal SoldQuantity,
    decimal WastedQuantity,
    decimal RemainingQuantity,
    BatchStatus Status);

public sealed record BatchPickerOption(
    Guid BatchId,
    Guid ProductId,
    string BatchReference,
    DateTime PurchaseDate,
    DateTime? ExpiryDate,
    decimal RemainingQuantity);

public sealed record InvoiceReceipt(
    string InvoiceNumber,
    DateTimeOffset InvoiceDate,
    SaleChannel SaleChannel,
    ReceiptCustomer? Customer,
    IReadOnlyCollection<InvoiceReceiptLine> Lines,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal GrandTotal,
    PaymentMethod PaymentMethod,
    decimal AmountPaid,
    decimal BalanceAmount,
    DateTime? DueDate);

public sealed record ReceiptCustomer(
    Guid CustomerId,
    string Name,
    string Phone,
    string WhatsAppNo,
    string? Address);

public sealed record InvoiceReceiptLine(
    string ProductName,
    string BatchReference,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineDiscount,
    decimal LineTotal);

public sealed record VoidInvoiceRequest(
    Guid InvoiceId,
    Guid UserId,
    UserRole Role,
    string Reason);

public sealed record VoidInvoiceResult(
    Guid InvoiceId,
    string InvoiceNumber,
    PaymentStatus PaymentStatus,
    string Reason);

public sealed record SalesReturnRequest(
    Guid InvoiceId,
    Guid ProcessedBy,
    UserRole Role,
    string Reason,
    PaymentMethod RefundMethod,
    IReadOnlyCollection<SalesReturnLine> Lines);

public sealed record SalesReturnLine(
    Guid InvoiceItemId,
    decimal Quantity);

public sealed record SalesReturnResult(
    Guid ReturnId,
    Guid InvoiceId,
    string InvoiceNumber,
    PaymentStatus PaymentStatus,
    bool IsFullReturn,
    decimal ReturnValue,
    decimal RefundAmount,
    decimal BalanceReduction,
    decimal RemainingInvoiceBalance,
    IReadOnlyCollection<Guid> WastageRecordIds);

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
    string RecipientWhatsAppNo,
    string MessageBody,
    DateTimeOffset ScheduledAt);

public sealed record QueuedWhatsAppMessage(
    Guid NotificationId,
    string RecipientWhatsAppNo,
    string MessageBody);

public sealed record PaymentReminderRunRequest(
    DateOnly AsOfDate,
    int ReminderLeadDays);

public sealed record NotificationRunRequest(
    DateOnly AsOfDate);

public sealed record OwnerDailySummaryRequest(
    DateOnly BusinessDate,
    string OwnerWhatsAppNumber,
    TimeOnly ReportTime);

public sealed record UpdateNotificationTemplateRequest(
    NotificationType NotificationType,
    string TemplateBody,
    Guid UpdatedBy,
    UserRole Role);

public sealed record NotificationTemplateResult(
    NotificationType NotificationType,
    string TemplateBody,
    DateTimeOffset UpdatedAt);

public sealed record NotificationLogQuery(
    Guid? CustomerId = null,
    Guid? InvoiceId = null,
    NotificationType? NotificationType = null);

public sealed record NotificationLogEntry(
    Guid NotificationId,
    Guid? CustomerId,
    Guid? InvoiceId,
    NotificationType NotificationType,
    NotificationChannel Channel,
    NotificationStatus Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt,
    int RetryCount,
    string RecipientWhatsAppNo,
    string MessageBody);

public sealed record RetryFailedNotificationsRequest(
    int MaxRetryCount,
    int RetryIntervalMinutes);

public sealed record NotificationResult(
    Guid NotificationId,
    Guid? CustomerId,
    Guid? InvoiceId,
    NotificationType NotificationType,
    NotificationStatus Status,
    DateTimeOffset ScheduledAt,
    int RetryCount,
    string RecipientWhatsAppNo,
    string MessageBody);

public sealed record OwnerDailySummaryResult(
    DateOnly BusinessDate,
    decimal GrossProfit,
    decimal WastageValue,
    Guid? NotificationId,
    string MessageBody);

public sealed record SystemSettingsQuery(
    Guid RequestedBy,
    UserRole Role,
    bool RevealSecrets = false);

public sealed record UpdateSystemSettingsRequest(
    Guid UpdatedBy,
    UserRole Role,
    IReadOnlyCollection<SystemSettingUpdate> Settings);

public sealed record SystemSettingUpdate(
    string Key,
    string Value,
    SystemSettingValueType ValueType,
    bool IsSecret = false);

public sealed record SystemSettingResult(
    string Key,
    string Value,
    SystemSettingValueType ValueType,
    bool IsSecret,
    DateTimeOffset UpdatedAt);

public sealed record AdminOperationRequest(
    Guid UserId,
    UserRole Role);

public sealed record DatabaseBackupRequest(
    Guid UserId,
    UserRole Role,
    string? DestinationDirectory = null);

public sealed record DatabaseOperationResult(
    bool Succeeded,
    string Message);

public sealed record DatabaseBackupResult(
    bool Succeeded,
    string BackupPath,
    string Message);

public sealed record OfflineSyncQueueStatusResult(
    int Pending,
    int Processing,
    int Completed,
    int Failed);

public sealed record ReceiptPdfRequest(
    Guid InvoiceId,
    Guid RequestedBy,
    UserRole Role);

public sealed record ReceiptPdfResult(
    Guid InvoiceId,
    string InvoiceNumber,
    string FileName,
    byte[] PdfBytes);

public sealed record OperationalReportRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid RequestedBy,
    UserRole Role,
    Guid? ProductId = null,
    Guid? BatchId = null,
    Guid? CustomerId = null,
    Guid? CashierId = null,
    SaleChannel? SaleChannel = null,
    PaymentMethod? PaymentMethod = null);

public sealed record OperationalReportResult(
    DateOnly FromDate,
    DateOnly ToDate,
    SalesReportSummary Sales,
    ProfitReportSummary Profit,
    WastageReportSummary Wastage,
    IReadOnlyList<StockOnHandReportLine> StockOnHand,
    IReadOnlyList<BatchMovementReportLine> BatchMovements,
    DebtorAgingReport DebtorAging,
    PaymentCollectionReportSummary Payments,
    SalesReturnReportSummary SalesReturns,
    IReadOnlyList<AuditLogEntryResult> AuditActivity);

public sealed record SalesReportSummary(
    decimal TotalSales,
    decimal RetailSales,
    decimal WholesaleSales,
    int InvoiceCount);

public sealed record ProfitReportSummary(
    decimal GrossSales,
    decimal CostOfGoodsSold,
    decimal GrossProfit);

public sealed record WastageReportSummary(
    decimal TotalWastageValue,
    decimal CustomerReturnWastageValue,
    int RecordCount);

public sealed record StockOnHandReportLine(
    Guid ProductId,
    string ProductName,
    string UnitOfMeasure,
    decimal RemainingQuantity,
    decimal ReorderLevel);

public sealed record BatchMovementReportLine(
    Guid BatchId,
    Guid ProductId,
    string ProductName,
    decimal PurchasedQuantity,
    decimal SoldQuantity,
    decimal WastedQuantity,
    decimal RemainingQuantity);

public sealed record PaymentCollectionReportSummary(
    decimal TotalCash,
    decimal TotalCard,
    decimal TotalRefunds,
    int PaymentCount);

public sealed record SalesReturnReportSummary(
    decimal TotalReturnValue,
    decimal TotalRefundAmount,
    int ReturnCount);

public sealed record AuditLogQuery(
    Guid RequestedBy,
    UserRole Role,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? UserId = null,
    string? Module = null,
    string? Action = null);

public sealed record AuditLogEntryResult(
    Guid LogId,
    Guid UserId,
    UserRole Role,
    string Action,
    string Module,
    string TargetEntity,
    Guid TargetId,
    DateTimeOffset Timestamp);

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
