namespace BlueBirdsERP.Domain.Enums;

public enum UserRole
{
    Admin,
    Cashier
}

public enum RbacPermission
{
    PosBilling,
    PaymentRecording,
    SalesReturns,
    CustomerReadOnlyLookup,
    InventoryManagement,
    BatchManagement,
    CustomerAccountManagement,
    CreditManagement,
    WhatsAppConfiguration,
    Reporting,
    UserManagement,
    SystemConfiguration,
    AuditLogRead
}

public enum CustomerType
{
    Retail,
    Wholesale
}

public enum AccountType
{
    None,
    BusinessAccount,
    OneTimeCreditor
}

public enum BusinessAccountStatus
{
    Active,
    Suspended
}

public enum SaleChannel
{
    Retail,
    Wholesale
}

public enum PaymentMethod
{
    Cash,
    Card,
    Credit,
    Mixed
}

public enum PaymentKind
{
    Payment,
    Refund
}

public enum PaymentStatus
{
    Paid,
    Partial,
    Pending,
    Void
}

public enum PricingType
{
    WeightBased,
    UnitBased
}

public enum BatchStatus
{
    Active,
    Exhausted,
    Expired,
    Recalled
}

public enum WastageType
{
    Expiry,
    DamagedPackaging,
    CustomerReturn,
    Other
}

public enum NotificationType
{
    PaymentReminder,
    OverdueAlert,
    OwnerDailyReport
}

public enum NotificationChannel
{
    WhatsApp
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

public enum SystemSettingValueType
{
    String,
    Integer,
    Decimal,
    Boolean,
    Time,
    EncryptedString
}

public enum OfflineSyncStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
