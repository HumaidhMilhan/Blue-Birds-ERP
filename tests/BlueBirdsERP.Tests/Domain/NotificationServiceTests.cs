using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.Notifications;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task Payment_reminder_window_queues_both_creditor_account_types_once()
    {
        var harness = CreateHarness();
        var businessCustomer = harness.Store.AddCustomer("ABC Hotels", AccountType.BusinessAccount, "+94771110000");
        var oneTimeCreditor = harness.Store.AddCustomer("Kamal Perera", AccountType.OneTimeCreditor, "+94772220000");
        var businessInvoice = harness.Store.AddInvoice(
            businessCustomer.CustomerId,
            "INV-20260428W-0001",
            dueDate: new DateTime(2026, 5, 1),
            balance: 1_250m,
            paymentMethod: PaymentMethod.Credit);
        harness.Store.AddInvoice(
            oneTimeCreditor.CustomerId,
            "INV-20260428W-0002",
            dueDate: new DateTime(2026, 4, 30),
            balance: 800m,
            paymentMethod: PaymentMethod.Mixed);
        harness.Store.AddInvoice(
            businessCustomer.CustomerId,
            "INV-20260428W-0003",
            dueDate: new DateTime(2026, 5, 5),
            balance: 500m,
            paymentMethod: PaymentMethod.Credit);

        var result = await harness.Service.QueuePaymentRemindersAsync(new PaymentReminderRunRequest(
            new DateOnly(2026, 4, 28),
            ReminderLeadDays: 3));
        var duplicateRun = await harness.Service.QueuePaymentRemindersAsync(new PaymentReminderRunRequest(
            new DateOnly(2026, 4, 28),
            ReminderLeadDays: 3));
        var log = await harness.Service.GetNotificationLogAsync(new NotificationLogQuery(
            businessCustomer.CustomerId,
            businessInvoice.InvoiceId,
            NotificationType.PaymentReminder));

        Assert.Equal(2, result.Count);
        Assert.Empty(duplicateRun);
        Assert.Contains(result, notification => notification.MessageBody.Contains("ABC Hotels", StringComparison.Ordinal));
        Assert.Contains(result, notification => notification.MessageBody.Contains("INV-20260428W-0002", StringComparison.Ordinal));
        Assert.Single(log);
        Assert.Equal(NotificationStatus.Pending, log[0].Status);
        Assert.Equal(0, log[0].RetryCount);
        Assert.Equal("+94771110000", log[0].RecipientWhatsAppNo);
    }

    [Fact]
    public async Task Overdue_reminders_queue_daily_until_invoice_is_settled()
    {
        var harness = CreateHarness();
        var businessCustomer = harness.Store.AddCustomer("ABC Hotels", AccountType.BusinessAccount, "+94771110000");
        var oneTimeCreditor = harness.Store.AddCustomer("Kamal Perera", AccountType.OneTimeCreditor, "+94772220000");
        var settledLater = harness.Store.AddInvoice(
            businessCustomer.CustomerId,
            "INV-20260420W-0001",
            dueDate: new DateTime(2026, 4, 25),
            balance: 1_000m,
            paymentMethod: PaymentMethod.Credit);
        harness.Store.AddInvoice(
            oneTimeCreditor.CustomerId,
            "INV-20260420W-0002",
            dueDate: new DateTime(2026, 4, 26),
            balance: 500m,
            paymentMethod: PaymentMethod.Mixed);

        var firstRun = await harness.Service.QueueOverdueRemindersAsync(new NotificationRunRequest(new DateOnly(2026, 4, 28)));
        var duplicateSameDay = await harness.Service.QueueOverdueRemindersAsync(new NotificationRunRequest(new DateOnly(2026, 4, 28)));
        settledLater.BalanceAmount = 0m;
        settledLater.PaymentStatus = PaymentStatus.Paid;
        var nextDay = await harness.Service.QueueOverdueRemindersAsync(new NotificationRunRequest(new DateOnly(2026, 4, 29)));

        Assert.Equal(2, firstRun.Count);
        Assert.Empty(duplicateSameDay);
        Assert.Single(nextDay);
        Assert.Equal(oneTimeCreditor.CustomerId, nextDay[0].CustomerId);
    }

    [Fact]
    public async Task Admin_can_configure_templates_with_supported_placeholders()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer("ABC Hotels", AccountType.BusinessAccount, "+94771110000");
        harness.Store.AddInvoice(
            customer.CustomerId,
            "INV-20260428W-0001",
            dueDate: new DateTime(2026, 4, 29),
            balance: 1_250m,
            paymentMethod: PaymentMethod.Credit);

        await harness.Service.UpdateTemplateAsync(new UpdateNotificationTemplateRequest(
            NotificationType.PaymentReminder,
            "Pay {{invoice_number}} amount {{amount_due}} before {{due_date}}.",
            harness.AdminId,
            UserRole.Admin));

        var result = await harness.Service.QueuePaymentRemindersAsync(new PaymentReminderRunRequest(
            new DateOnly(2026, 4, 28),
            ReminderLeadDays: 2));

        Assert.Single(result);
        Assert.Equal("Pay INV-20260428W-0001 amount 1250.00 before 2026-04-29.", result[0].MessageBody);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("NOTIFICATION_TEMPLATE_UPDATE", harness.AuditLogger.Entries[0].Action);
        await Assert.ThrowsAsync<NotificationValidationException>(() => harness.Service.UpdateTemplateAsync(new UpdateNotificationTemplateRequest(
            NotificationType.PaymentReminder,
            "Hello {{unknown}}",
            harness.AdminId,
            UserRole.Admin)));
    }

    [Fact]
    public async Task Owner_daily_summary_queues_profit_and_wastage_values()
    {
        var harness = CreateHarness();
        var product = harness.Store.AddProduct();
        var batch = harness.Store.AddBatch(product.ProductId, costPrice: 300m);
        harness.Store.AddSalesInvoice(
            "INV-20260428R-0001",
            new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero),
            batch.BatchId,
            product.ProductId,
            quantity: 2m,
            lineTotal: 1_000m);
        harness.Store.WastageRecords.Add(new WastageRecord
        {
            BatchId = batch.BatchId,
            ProductId = product.ProductId,
            WastageDate = new DateTime(2026, 4, 28),
            Quantity = 1m,
            WastageType = WastageType.Other,
            EstimatedLoss = 150m
        });

        var result = await harness.Service.QueueOwnerDailySummaryAsync(new OwnerDailySummaryRequest(
            new DateOnly(2026, 4, 28),
            "+94770000000",
            new TimeOnly(20, 0)));
        var duplicate = await harness.Service.QueueOwnerDailySummaryAsync(new OwnerDailySummaryRequest(
            new DateOnly(2026, 4, 28),
            "+94770000000",
            new TimeOnly(20, 0)));

        Assert.Equal(400m, result.GrossProfit);
        Assert.Equal(150m, result.WastageValue);
        Assert.Contains("400.00", result.MessageBody, StringComparison.Ordinal);
        Assert.Contains("150.00", result.MessageBody, StringComparison.Ordinal);
        Assert.NotNull(result.NotificationId);
        Assert.Null(duplicate.NotificationId);
    }

    [Fact]
    public async Task Retry_failed_requeues_due_notifications_with_configured_retry_policy()
    {
        var harness = CreateHarness();
        var retryable = harness.Store.AddFailedNotification(retryCount: 1, nextRetryAt: harness.Clock.Now.AddMinutes(-1));
        var exhausted = harness.Store.AddFailedNotification(retryCount: 3, nextRetryAt: harness.Clock.Now.AddMinutes(-1));

        var result = await harness.Service.RetryFailedAsync(new RetryFailedNotificationsRequest(
            MaxRetryCount: 3,
            RetryIntervalMinutes: 10));

        Assert.Equal(1, result);
        Assert.Equal(NotificationStatus.Pending, retryable.Status);
        Assert.Equal(2, retryable.RetryCount);
        Assert.Equal(harness.Clock.Now.AddMinutes(10), retryable.ScheduledAt);
        Assert.Equal(NotificationStatus.Failed, exhausted.Status);
        Assert.Equal(3, exhausted.RetryCount);
    }

    private static TestHarness CreateHarness()
    {
        return new TestHarness();
    }

    private sealed class TestHarness
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero));
        public InMemoryNotificationDataStore Store { get; } = new();
        public RecordingAuditLogger AuditLogger { get; } = new();
        public NotificationService Service { get; }

        public TestHarness()
        {
            Service = new NotificationService(
                Store,
                new InlineTransactionRunner(),
                AuditLogger,
                Clock);
        }
    }

    private sealed class InMemoryNotificationDataStore : INotificationDataStore
    {
        private readonly Dictionary<Guid, Customer> _customers = [];
        private readonly Dictionary<Guid, Batch> _batches = [];
        private readonly Dictionary<NotificationType, NotificationTemplate> _templates = [];

        public List<Invoice> Invoices { get; } = [];
        public List<Notification> Notifications { get; } = [];
        public List<WastageRecord> WastageRecords { get; } = [];

        public Customer AddCustomer(string name, AccountType accountType, string whatsAppNo)
        {
            var customer = new Customer
            {
                Name = name,
                Phone = "0110000000",
                WhatsAppNo = whatsAppNo,
                CustomerType = CustomerType.Wholesale,
                AccountType = accountType
            };
            _customers[customer.CustomerId] = customer;
            return customer;
        }

        public Product AddProduct()
        {
            return new Product
            {
                Name = "Chicken",
                PricingType = PricingType.WeightBased,
                UnitOfMeasure = "Kg",
                SellingPrice = 500m,
                IsActive = true
            };
        }

        public Batch AddBatch(Guid productId, decimal costPrice)
        {
            var batch = new Batch
            {
                ProductId = productId,
                PurchaseDate = new DateTime(2026, 4, 20),
                InitialQuantity = 10m,
                RemainingQuantity = 10m,
                CostPrice = costPrice,
                Status = BatchStatus.Active
            };
            _batches[batch.BatchId] = batch;
            return batch;
        }

        public Invoice AddInvoice(
            Guid customerId,
            string invoiceNumber,
            DateTime dueDate,
            decimal balance,
            PaymentMethod paymentMethod)
        {
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                CustomerId = customerId,
                CashierId = Guid.NewGuid(),
                SaleChannel = SaleChannel.Wholesale,
                PaymentMethod = paymentMethod,
                InvoiceDate = new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero),
                DueDate = dueDate,
                GrandTotal = balance,
                BalanceAmount = balance,
                PaymentStatus = balance == 0m ? PaymentStatus.Paid : PaymentStatus.Partial
            };
            Invoices.Add(invoice);
            return invoice;
        }

        public Invoice AddSalesInvoice(
            string invoiceNumber,
            DateTimeOffset invoiceDate,
            Guid batchId,
            Guid productId,
            decimal quantity,
            decimal lineTotal)
        {
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                CashierId = Guid.NewGuid(),
                SaleChannel = SaleChannel.Retail,
                PaymentMethod = PaymentMethod.Cash,
                InvoiceDate = invoiceDate,
                GrandTotal = lineTotal,
                PaidAmount = lineTotal,
                PaymentStatus = PaymentStatus.Paid
            };
            invoice.Items.Add(new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                BatchId = batchId,
                ProductId = productId,
                ProductName = "Chicken",
                BatchReference = "BATCH-1",
                UnitOfMeasure = "Kg",
                Quantity = quantity,
                UnitPrice = lineTotal / quantity,
                LineTotal = lineTotal
            });
            Invoices.Add(invoice);
            return invoice;
        }

        public Notification AddFailedNotification(int retryCount, DateTimeOffset nextRetryAt)
        {
            var notification = new Notification
            {
                NotificationType = NotificationType.PaymentReminder,
                Channel = NotificationChannel.WhatsApp,
                RecipientWhatsAppNo = "+94771110000",
                MessageBody = "Failed message",
                CreatedAt = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero),
                ScheduledAt = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero),
                Status = NotificationStatus.Failed,
                RetryCount = retryCount,
                NextRetryAt = nextRetryAt,
                FailureReason = "Gateway unavailable"
            };
            Notifications.Add(notification);
            return notification;
        }

        public Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task UpdateNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Notification>> GetNotificationLogAsync(
            Guid? customerId,
            Guid? invoiceId,
            NotificationType? notificationType,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Notification> result = Notifications
                .Where(notification => !customerId.HasValue || notification.CustomerId == customerId)
                .Where(notification => !invoiceId.HasValue || notification.InvoiceId == invoiceId)
                .Where(notification => !notificationType.HasValue || notification.NotificationType == notificationType)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<bool> HasNotificationAsync(
            Guid? customerId,
            Guid? invoiceId,
            NotificationType notificationType,
            DateOnly? scheduledDate = null,
            CancellationToken cancellationToken = default)
        {
            var exists = Notifications
                .Where(notification => notification.CustomerId == customerId)
                .Where(notification => notification.InvoiceId == invoiceId)
                .Where(notification => notification.NotificationType == notificationType)
                .Any(notification => !scheduledDate.HasValue ||
                    DateOnly.FromDateTime(notification.ScheduledAt.Date) == scheduledDate.Value);

            return Task.FromResult(exists);
        }

        public Task<IReadOnlyList<Notification>> GetFailedNotificationsDueForRetryAsync(
            DateTimeOffset asOf,
            int maxRetryCount,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Notification> result = Notifications
                .Where(notification => notification.Status == NotificationStatus.Failed)
                .Where(notification => notification.RetryCount < maxRetryCount)
                .Where(notification => !notification.NextRetryAt.HasValue || notification.NextRetryAt <= asOf)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<NotificationTemplate?> GetNotificationTemplateAsync(NotificationType notificationType, CancellationToken cancellationToken = default)
        {
            _templates.TryGetValue(notificationType, out var template);
            return Task.FromResult(template);
        }

        public Task UpsertNotificationTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
        {
            _templates[template.NotificationType] = template;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Invoice>> GetOutstandingCreditInvoicesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Invoice> result = Invoices
                .Where(invoice => invoice.BalanceAmount > 0 && invoice.PaymentStatus != PaymentStatus.Void)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Invoice>> GetInvoicesForDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Invoice> result = Invoices
                .Where(invoice => DateOnly.FromDateTime(invoice.InvoiceDate.Date) == businessDate)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<WastageRecord>> GetWastageRecordsForDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WastageRecord> result = WastageRecords
                .Where(record => DateOnly.FromDateTime(record.WastageDate.Date) == businessDate)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            _customers.TryGetValue(customerId, out var customer);
            return Task.FromResult(customer);
        }

        public Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            _batches.TryGetValue(batchId, out var batch);
            return Task.FromResult(batch);
        }
    }

    private sealed class InlineTransactionRunner : ITransactionRunner
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset Now { get; } = now;
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
