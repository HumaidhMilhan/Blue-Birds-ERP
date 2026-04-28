namespace BlueBirdsERP.Infrastructure.Configuration;

public sealed class InfrastructureOptions
{
    public string EnvironmentName { get; set; } = "Production";
    public DatabaseOptions Database { get; set; } = new();
    public TwilioOptions Twilio { get; set; } = new();
    public NotificationOptions Notifications { get; set; } = new();
    public ReceiptOptions Receipt { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
    public RecoveryOptions Recovery { get; set; } = new();
    public DevelopmentBootstrapOptions DevelopmentBootstrap { get; set; } = new();
}

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "SQLite";
    public string CentralConnectionString { get; set; } = string.Empty;
    public string LocalPosConnectionString { get; set; } = "Data Source=bluebirds-mvp.sqlite3";
    public string BackupDirectory { get; set; } = "backups";
}

public sealed class TwilioOptions
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string WhatsAppSender { get; set; } = string.Empty;
}

public sealed class NotificationOptions
{
    public string OwnerWhatsAppNumber { get; set; } = string.Empty;
    public TimeOnly DailyReportTime { get; set; } = new(20, 0);
    public int PaymentReminderLeadDays { get; set; } = 3;
    public int MaxRetryCount { get; set; } = 3;
    public int RetryIntervalMinutes { get; set; } = 10;
}

public sealed class ReceiptOptions
{
    public string CompanyName { get; set; } = "Blue Birds Poultry";
    public string Header { get; set; } = "PoultryPro ERP";
    public string Footer { get; set; } = "Thank you";
    public int WidthMillimeters { get; set; } = 80;
}

public sealed class SecurityOptions
{
    public string EncryptionKey { get; set; } = string.Empty;
}

public sealed class RecoveryOptions
{
    public bool Enabled { get; set; }
    public string RecoveryKeySha256 { get; set; } = string.Empty;
    public string DefaultRecoveryAdminUsername { get; set; } = "RecoveryAdmin";
}

public sealed class DevelopmentBootstrapOptions
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
