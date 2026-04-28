using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Domain.Entities;

public sealed class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLogin { get; set; }
}

public sealed class Customer
{
    public Guid CustomerId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string WhatsAppNo { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public CustomerType CustomerType { get; set; }
    public AccountType AccountType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BusinessAccount
{
    public Guid AccountId { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public decimal CreditLimit { get; set; }
    public int CreditPeriodDays { get; set; }
    public int NotificationLeadDays { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Supplier
{
    public Guid SupplierId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PaymentTerms { get; set; }
}

public sealed class ProductCategory
{
    public Guid CategoryId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class Product
{
    public Guid ProductId { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PricingType PricingType { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Batch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal InitialQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Active;
}

public sealed class Invoice
{
    public Guid InvoiceId { get; set; } = Guid.NewGuid();
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Guid CashierId { get; set; }
    public SaleChannel SaleChannel { get; set; }
    public DateTimeOffset InvoiceDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTime? DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? Notes { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public sealed class InvoiceItem
{
    public Guid ItemId { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid ProductId { get; set; }
    public Guid BatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class Payment
{
    public Guid PaymentId { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset PaymentDate { get; set; } = DateTimeOffset.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public Guid RecordedBy { get; set; }
}

public sealed class WastageRecord
{
    public Guid WastageId { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime WastageDate { get; set; }
    public decimal Quantity { get; set; }
    public WastageType WastageType { get; set; }
    public Guid? RelatedReturnId { get; set; }
    public decimal EstimatedLoss { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedBy { get; set; }
}

public sealed class SalesReturn
{
    public Guid ReturnId { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset ReturnDate { get; set; } = DateTimeOffset.UtcNow;
    public string Reason { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public Guid ProcessedBy { get; set; }
}

public sealed class Notification
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();
    public Guid? CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public NotificationType NotificationType { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public string MessageBody { get; set; } = string.Empty;
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int RetryCount { get; set; }
}

public sealed class AuditLog
{
    public Guid LogId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string? BeforeValueJson { get; set; }
    public string? AfterValueJson { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

