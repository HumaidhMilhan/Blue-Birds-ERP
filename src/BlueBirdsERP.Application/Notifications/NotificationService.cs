using System.Globalization;
using System.Text.RegularExpressions;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Notifications;

public sealed partial class NotificationService(
    INotificationDataStore dataStore,
    ITransactionRunner transactionRunner,
    IAuditLogger auditLogger,
    ISystemClock clock) : INotificationService
{
    private static readonly IReadOnlyDictionary<NotificationType, string> DefaultTemplates =
        new Dictionary<NotificationType, string>
        {
            [NotificationType.PaymentReminder] = "Hello {{customer_name}}, invoice {{invoice_number}} has {{amount_due}} due on {{due_date}}.",
            [NotificationType.OverdueAlert] = "Hello {{customer_name}}, invoice {{invoice_number}} is overdue. Amount due: {{amount_due}}, due date: {{due_date}}.",
            [NotificationType.OwnerDailyReport] = "PoultryPro daily summary for {{report_date}}: gross profit {{gross_profit}}, wastage {{wastage_value}}."
        };

    private static readonly IReadOnlyDictionary<NotificationType, IReadOnlySet<string>> AllowedPlaceholders =
        new Dictionary<NotificationType, IReadOnlySet<string>>
        {
            [NotificationType.PaymentReminder] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "customer_name",
                "invoice_number",
                "amount_due",
                "due_date"
            },
            [NotificationType.OverdueAlert] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "customer_name",
                "invoice_number",
                "amount_due",
                "due_date"
            },
            [NotificationType.OwnerDailyReport] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "report_date",
                "gross_profit",
                "wastage_value"
            }
        };

    public Task QueueAsync(NotificationEnvelope notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.RecipientWhatsAppNo))
        {
            throw new NotificationValidationException("Recipient WhatsApp number is required.");
        }

        if (string.IsNullOrWhiteSpace(notification.MessageBody))
        {
            throw new NotificationValidationException("Message body is required.");
        }

        var queuedNotification = new Notification
        {
            CustomerId = notification.CustomerId,
            InvoiceId = notification.InvoiceId,
            NotificationType = notification.NotificationType,
            Channel = NotificationChannel.WhatsApp,
            RecipientWhatsAppNo = notification.RecipientWhatsAppNo.Trim(),
            MessageBody = notification.MessageBody.Trim(),
            CreatedAt = clock.Now,
            ScheduledAt = notification.ScheduledAt,
            Status = NotificationStatus.Pending
        };

        return dataStore.AddNotificationAsync(queuedNotification, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResult>> QueuePaymentRemindersAsync(
        PaymentReminderRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ReminderLeadDays < 0)
        {
            throw new NotificationValidationException("Reminder lead days cannot be negative.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var template = await GetTemplateBodyAsync(NotificationType.PaymentReminder, token);
            var outstandingInvoices = await dataStore.GetOutstandingCreditInvoicesAsync(token);
            var latestReminderDueDate = request.AsOfDate.AddDays(request.ReminderLeadDays);
            var queued = new List<NotificationResult>();

            foreach (var invoice in outstandingInvoices
                .Where(invoice => IsOutstandingCreditInvoice(invoice) && invoice.DueDate.HasValue)
                .Where(invoice =>
                {
                    var dueDate = DateOnly.FromDateTime(invoice.DueDate!.Value.Date);
                    return dueDate >= request.AsOfDate && dueDate <= latestReminderDueDate;
                })
                .OrderBy(invoice => invoice.DueDate)
                .ThenBy(invoice => invoice.InvoiceNumber))
            {
                if (!invoice.CustomerId.HasValue)
                {
                    continue;
                }

                if (await dataStore.HasNotificationAsync(
                    invoice.CustomerId,
                    invoice.InvoiceId,
                    NotificationType.PaymentReminder,
                    scheduledDate: null,
                    token))
                {
                    continue;
                }

                var customer = await dataStore.GetCustomerAsync(invoice.CustomerId.Value, token);
                if (!CanNotifyCreditor(customer))
                {
                    continue;
                }

                var notification = CreateInvoiceNotification(
                    NotificationType.PaymentReminder,
                    customer!,
                    invoice,
                    template);
                await dataStore.AddNotificationAsync(notification, token);
                queued.Add(CreateResult(notification));
            }

            return queued;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResult>> QueueOverdueRemindersAsync(
        NotificationRunRequest request,
        CancellationToken cancellationToken = default)
    {
        return await transactionRunner.ExecuteAsync(async token =>
        {
            var template = await GetTemplateBodyAsync(NotificationType.OverdueAlert, token);
            var outstandingInvoices = await dataStore.GetOutstandingCreditInvoicesAsync(token);
            var queued = new List<NotificationResult>();

            foreach (var invoice in outstandingInvoices
                .Where(invoice => IsOutstandingCreditInvoice(invoice) && invoice.DueDate.HasValue)
                .Where(invoice => DateOnly.FromDateTime(invoice.DueDate!.Value.Date) < request.AsOfDate)
                .OrderBy(invoice => invoice.DueDate)
                .ThenBy(invoice => invoice.InvoiceNumber))
            {
                if (!invoice.CustomerId.HasValue)
                {
                    continue;
                }

                if (await dataStore.HasNotificationAsync(
                    invoice.CustomerId,
                    invoice.InvoiceId,
                    NotificationType.OverdueAlert,
                    request.AsOfDate,
                    token))
                {
                    continue;
                }

                var customer = await dataStore.GetCustomerAsync(invoice.CustomerId.Value, token);
                if (!CanNotifyCreditor(customer))
                {
                    continue;
                }

                var notification = CreateInvoiceNotification(
                    NotificationType.OverdueAlert,
                    customer!,
                    invoice,
                    template);
                await dataStore.AddNotificationAsync(notification, token);
                queued.Add(CreateResult(notification));
            }

            return queued;
        }, cancellationToken);
    }

    public async Task<OwnerDailySummaryResult> QueueOwnerDailySummaryAsync(
        OwnerDailySummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OwnerWhatsAppNumber))
        {
            throw new NotificationValidationException("Owner WhatsApp number is required.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var template = await GetTemplateBodyAsync(NotificationType.OwnerDailyReport, token);
            var invoices = await dataStore.GetInvoicesForDateAsync(request.BusinessDate, token);
            var wastageRecords = await dataStore.GetWastageRecordsForDateAsync(request.BusinessDate, token);
            var grossProfit = await CalculateGrossProfitAsync(invoices, token);
            var wastageValue = wastageRecords.Sum(record => record.EstimatedLoss);
            var message = RenderTemplate(template, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["report_date"] = FormatDate(request.BusinessDate),
                ["gross_profit"] = FormatMoney(grossProfit),
                ["wastage_value"] = FormatMoney(wastageValue)
            });

            if (await dataStore.HasNotificationAsync(
                customerId: null,
                invoiceId: null,
                NotificationType.OwnerDailyReport,
                request.BusinessDate,
                token))
            {
                return new OwnerDailySummaryResult(
                    request.BusinessDate,
                    grossProfit,
                    wastageValue,
                    NotificationId: null,
                    message);
            }

            var scheduledAt = new DateTimeOffset(
                request.BusinessDate.ToDateTime(request.ReportTime),
                clock.Now.Offset);
            var notification = new Notification
            {
                NotificationType = NotificationType.OwnerDailyReport,
                Channel = NotificationChannel.WhatsApp,
                RecipientWhatsAppNo = request.OwnerWhatsAppNumber.Trim(),
                MessageBody = message,
                CreatedAt = clock.Now,
                ScheduledAt = scheduledAt,
                Status = NotificationStatus.Pending
            };

            await dataStore.AddNotificationAsync(notification, token);

            return new OwnerDailySummaryResult(
                request.BusinessDate,
                grossProfit,
                wastageValue,
                notification.NotificationId,
                notification.MessageBody);
        }, cancellationToken);
    }

    public async Task<NotificationTemplateResult> UpdateTemplateAsync(
        UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role != UserRole.Admin)
        {
            throw new NotificationValidationException("Only Admin users can update notification templates.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateBody))
        {
            throw new NotificationValidationException("Template body is required.");
        }

        ValidateTemplatePlaceholders(request.NotificationType, request.TemplateBody);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var existingTemplate = await dataStore.GetNotificationTemplateAsync(request.NotificationType, token);
            var before = existingTemplate is null
                ? null
                : $"{{\"notificationType\":\"{existingTemplate.NotificationType}\",\"templateBody\":\"{EscapeJson(existingTemplate.TemplateBody)}\"}}";
            var template = existingTemplate ?? new NotificationTemplate
            {
                NotificationType = request.NotificationType
            };

            template.TemplateBody = request.TemplateBody.Trim();
            template.UpdatedBy = request.UpdatedBy;
            template.UpdatedAt = clock.Now;
            await dataStore.UpsertNotificationTemplateAsync(template, token);

            await auditLogger.WriteAsync(new AuditEntry(
                request.UpdatedBy,
                request.Role,
                "NOTIFICATION_TEMPLATE_UPDATE",
                "NOTIFICATIONS",
                nameof(NotificationTemplate),
                template.TemplateId,
                before,
                $"{{\"notificationType\":\"{template.NotificationType}\",\"templateBody\":\"{EscapeJson(template.TemplateBody)}\"}}"), token);

            return new NotificationTemplateResult(
                template.NotificationType,
                template.TemplateBody,
                template.UpdatedAt);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationLogEntry>> GetNotificationLogAsync(
        NotificationLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var notifications = await dataStore.GetNotificationLogAsync(
            query.CustomerId,
            query.InvoiceId,
            query.NotificationType,
            cancellationToken);

        return notifications
            .OrderByDescending(notification => notification.ScheduledAt)
            .ThenByDescending(notification => notification.CreatedAt)
            .Select(CreateLogEntry)
            .ToList();
    }

    public async Task<int> RetryFailedAsync(
        RetryFailedNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MaxRetryCount < 3)
        {
            throw new NotificationValidationException("Maximum retry count must be at least 3.");
        }

        if (request.RetryIntervalMinutes <= 0)
        {
            throw new NotificationValidationException("Retry interval must be greater than zero minutes.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var failedNotifications = await dataStore.GetFailedNotificationsDueForRetryAsync(
                clock.Now,
                request.MaxRetryCount,
                token);
            var retryCount = 0;

            foreach (var notification in failedNotifications
                .Where(notification => notification.Status == NotificationStatus.Failed)
                .Where(notification => notification.RetryCount < request.MaxRetryCount)
                .Where(notification => !notification.NextRetryAt.HasValue || notification.NextRetryAt <= clock.Now))
            {
                notification.Status = NotificationStatus.Pending;
                notification.RetryCount++;
                notification.ScheduledAt = clock.Now.AddMinutes(request.RetryIntervalMinutes);
                notification.NextRetryAt = null;
                notification.FailureReason = null;
                await dataStore.UpdateNotificationAsync(notification, token);
                retryCount++;
            }

            return retryCount;
        }, cancellationToken);
    }

    private async Task<string> GetTemplateBodyAsync(NotificationType notificationType, CancellationToken cancellationToken)
    {
        var template = await dataStore.GetNotificationTemplateAsync(notificationType, cancellationToken);
        return template?.TemplateBody ?? DefaultTemplates[notificationType];
    }

    private Notification CreateInvoiceNotification(
        NotificationType notificationType,
        Customer customer,
        Invoice invoice,
        string template)
    {
        var dueDate = DateOnly.FromDateTime(invoice.DueDate!.Value.Date);
        var message = RenderTemplate(template, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customer_name"] = customer.Name,
            ["invoice_number"] = invoice.InvoiceNumber,
            ["amount_due"] = FormatMoney(invoice.BalanceAmount),
            ["due_date"] = FormatDate(dueDate)
        });

        return new Notification
        {
            CustomerId = customer.CustomerId,
            InvoiceId = invoice.InvoiceId,
            NotificationType = notificationType,
            Channel = NotificationChannel.WhatsApp,
            RecipientWhatsAppNo = customer.WhatsAppNo.Trim(),
            MessageBody = message,
            CreatedAt = clock.Now,
            ScheduledAt = clock.Now,
            Status = NotificationStatus.Pending
        };
    }

    private async Task<decimal> CalculateGrossProfitAsync(
        IReadOnlyList<Invoice> invoices,
        CancellationToken cancellationToken)
    {
        var grossProfit = 0m;

        foreach (var invoice in invoices.Where(invoice => invoice.PaymentStatus != PaymentStatus.Void))
        {
            foreach (var item in invoice.Items)
            {
                var batch = await dataStore.GetBatchAsync(item.BatchId, cancellationToken)
                    ?? throw new NotificationValidationException("Invoice batch was not found for profit calculation.");
                grossProfit += item.LineTotal - item.Quantity * batch.CostPrice;
            }
        }

        return grossProfit;
    }

    private static bool IsOutstandingCreditInvoice(Invoice invoice)
    {
        return invoice.PaymentStatus != PaymentStatus.Void &&
            invoice.BalanceAmount > 0 &&
            invoice.CustomerId.HasValue &&
            invoice.PaymentMethod is PaymentMethod.Credit or PaymentMethod.Mixed;
    }

    private static bool CanNotifyCreditor(Customer? customer)
    {
        return customer is not null &&
            customer.AccountType is AccountType.BusinessAccount or AccountType.OneTimeCreditor &&
            !string.IsNullOrWhiteSpace(customer.WhatsAppNo);
    }

    private static void ValidateTemplatePlaceholders(NotificationType notificationType, string templateBody)
    {
        if (!AllowedPlaceholders.TryGetValue(notificationType, out var allowedPlaceholders))
        {
            throw new NotificationValidationException("Unsupported notification template type.");
        }

        var unknownPlaceholders = PlaceholderRegex()
            .Matches(templateBody)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(placeholder => !allowedPlaceholders.Contains(placeholder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknownPlaceholders.Count > 0)
        {
            throw new NotificationValidationException($"Unknown template placeholder: {unknownPlaceholders[0]}.");
        }
    }

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string> variables)
    {
        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static NotificationResult CreateResult(Notification notification)
    {
        return new NotificationResult(
            notification.NotificationId,
            notification.CustomerId,
            notification.InvoiceId,
            notification.NotificationType,
            notification.Status,
            notification.ScheduledAt,
            notification.RetryCount,
            notification.RecipientWhatsAppNo,
            notification.MessageBody);
    }

    private static NotificationLogEntry CreateLogEntry(Notification notification)
    {
        return new NotificationLogEntry(
            notification.NotificationId,
            notification.CustomerId,
            notification.InvoiceId,
            notification.NotificationType,
            notification.Channel,
            notification.Status,
            notification.ScheduledAt,
            notification.SentAt,
            notification.RetryCount,
            notification.RecipientWhatsAppNo,
            notification.MessageBody);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")]
    private static partial Regex PlaceholderRegex();
}
