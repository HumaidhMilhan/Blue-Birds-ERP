namespace BlueBirdsERP.Infrastructure.Configuration;

public sealed class InfrastructureOptions
{
    public DatabaseOptions Database { get; init; } = new();
    public TwilioOptions Twilio { get; init; } = new();
    public NotificationOptions Notifications { get; init; } = new();
}

public sealed class DatabaseOptions
{
    public string Provider { get; init; } = "PostgreSQL";
    public string CentralConnectionString { get; init; } = string.Empty;
    public string LocalPosConnectionString { get; init; } = "Data Source=local-pos.sqlite3";
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
