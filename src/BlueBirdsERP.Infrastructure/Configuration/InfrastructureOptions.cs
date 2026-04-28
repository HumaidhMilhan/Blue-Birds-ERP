namespace BlueBirdsERP.Infrastructure.Configuration;

public sealed class InfrastructureOptions
{
    public string EnvironmentName { get; init; } = "Production";
    public DatabaseOptions Database { get; init; } = new();
    public TwilioOptions Twilio { get; init; } = new();
    public NotificationOptions Notifications { get; init; } = new();
    public ReceiptOptions Receipt { get; init; } = new();
    public SecurityOptions Security { get; init; } = new();
    public RecoveryOptions Recovery { get; init; } = new();
    public DevelopmentBootstrapOptions DevelopmentBootstrap { get; init; } = new();
}

public sealed class DatabaseOptions
{
    public string Provider { get; init; } = "SQLite";
    public string CentralConnectionString { get; init; } = string.Empty;
    public string LocalPosConnectionString { get; init; } = "Data Source=bluebirds-mvp.sqlite3";
    public string BackupDirectory { get; init; } = "backups";
}

public sealed class TwilioOptions
{
    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;
    public string WhatsAppSender { get; init; } = string.Empty;
}

public sealed class NotificationOptions
{
    public string OwnerWhatsAppNumber { get; init; } = string.Empty;
    public TimeOnly DailyReportTime { get; init; } = new(20, 0);
    public int PaymentReminderLeadDays { get; init; } = 3;
    public int MaxRetryCount { get; init; } = 3;
    public int RetryIntervalMinutes { get; init; } = 10;
}

public sealed class ReceiptOptions
{
    public string CompanyName { get; init; } = "Blue Birds Poultry";
    public string Header { get; init; } = "PoultryPro ERP";
    public string Footer { get; init; } = "Thank you";
    public int WidthMillimeters { get; init; } = 80;
}

public sealed class SecurityOptions
{
    public string EncryptionKey { get; init; } = string.Empty;
}

public sealed class RecoveryOptions
{
    public bool Enabled { get; init; }
    public string RecoveryKeySha256 { get; init; } = string.Empty;
    public string DefaultRecoveryAdminUsername { get; init; } = "RecoveryAdmin";
}

public sealed class DevelopmentBootstrapOptions
{
    public bool Enabled { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
