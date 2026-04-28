using System.Security.Cryptography;
using System.Text;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Application.Security;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Configuration;
using BlueBirdsERP.Infrastructure.Persistence;
using BlueBirdsERP.Infrastructure.Printing;
using BlueBirdsERP.Infrastructure.Reporting;
using BlueBirdsERP.Infrastructure.Security;
using BlueBirdsERP.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Tests.Domain;

public sealed class MvpBackendFinalizationTests
{
    [Fact]
    public async Task Development_bootstrap_is_unavailable_in_production_and_creates_admin_in_development()
    {
        await using var productionContext = CreateContext(out _);
        var productionOptions = CreateOptions(
            productionContext.Database.GetConnectionString()!,
            environmentName: "Production",
            developmentBootstrap: new DevelopmentBootstrapOptions
        {
            Enabled = true,
            Username = "Kratos",
            Password = "Kratossparta"
        });
        var productionStore = new EfCoreDataStore(productionContext);
        var productionService = new DevelopmentBootstrapService(
            productionOptions,
            productionStore,
            new TestPasswordHasher(),
            new EfAuditLogger(productionContext),
            new FixedClock());

        var productionResult = await productionService.EnsureDevelopmentBootstrapAsync();

        Assert.False(productionResult.Created);
        Assert.Empty(productionContext.Users);

        await using var devContext = CreateContext(out _);
        var devOptions = CreateOptions(
            devContext.Database.GetConnectionString()!,
            environmentName: "Development",
            developmentBootstrap: new DevelopmentBootstrapOptions
        {
            Enabled = true,
            Username = "Kratos",
            Password = "Kratossparta"
        });
        var devStore = new EfCoreDataStore(devContext);
        var devService = new DevelopmentBootstrapService(
            devOptions,
            devStore,
            new TestPasswordHasher(),
            new EfAuditLogger(devContext),
            new FixedClock());

        var devResult = await devService.EnsureDevelopmentBootstrapAsync();

        Assert.True(devResult.Created);
        Assert.Contains(devContext.Users, user => user.Username == "Kratos" && user.Role == UserRole.Admin);
        Assert.Contains(devContext.AuditLogs, log => log.Action == "DEV_BOOTSTRAP_ADMIN_CREATE");
    }

    [Fact]
    public async Task Break_glass_recovery_generates_temporary_admin_password_and_audits()
    {
        await using var context = CreateContext(out _);
        var recoveryKey = "local-recovery-key";
        var options = CreateOptions(
            context.Database.GetConnectionString()!,
            recovery: new RecoveryOptions
        {
            Enabled = true,
            RecoveryKeySha256 = Sha256(recoveryKey),
            DefaultRecoveryAdminUsername = "RecoveryAdmin"
        });
        var generator = new TestPasswordGenerator("Generated-Recovery");
        var service = new RecoveryAccessService(
            options,
            new EfCoreDataStore(context),
            new TestPasswordHasher(),
            generator,
            new EfAuditLogger(context),
            new FixedClock());

        var result = await service.GenerateTemporaryAdminPasswordAsync(new RecoveryAccessRequest(recoveryKey));

        var user = await context.Users.SingleAsync(user => user.UserId == result.AdminUserId);
        Assert.Equal("RecoveryAdmin", result.Username);
        Assert.Equal("Generated-Recovery", result.TemporaryPassword);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("HASH:Generated-Recovery", user.PasswordHash);
        Assert.Contains(context.AuditLogs, log => log.Action == "BREAK_GLASS_ADMIN_RECOVERY");
    }

    [Fact]
    public async Task Settings_service_encrypts_secret_values_and_rejects_cashier_access()
    {
        await using var context = CreateContext(out _);
        var options = CreateOptions(
            context.Database.GetConnectionString()!,
            security: new SecurityOptions { EncryptionKey = "unit-test-key" });
        var service = new SystemSettingsService(
            context,
            new EncryptedConfigurationStore(options),
            new EfAuditLogger(context));

        await service.UpdateSettingsAsync(new UpdateSystemSettingsRequest(
            Guid.NewGuid(),
            UserRole.Admin,
            [
                new SystemSettingUpdate("Database.CentralConnectionString", "Host=prod;Password=secret", SystemSettingValueType.EncryptedString, IsSecret: true),
                new SystemSettingUpdate("Receipt.Footer", "Thank you", SystemSettingValueType.String)
            ]));

        var storedSecret = await context.SystemSettings.SingleAsync(setting => setting.SettingKey == "Database.CentralConnectionString");
        var masked = await service.GetSettingsAsync(new SystemSettingsQuery(Guid.NewGuid(), UserRole.Admin));
        var revealed = await service.GetSettingsAsync(new SystemSettingsQuery(Guid.NewGuid(), UserRole.Admin, RevealSecrets: true));

        Assert.NotEqual("Host=prod;Password=secret", storedSecret.SettingValue);
        Assert.Equal("********", masked.Single(setting => setting.Key == "Database.CentralConnectionString").Value);
        Assert.Equal("Host=prod;Password=secret", revealed.Single(setting => setting.Key == "Database.CentralConnectionString").Value);
        Assert.Contains(context.AuditLogs, log => log.Action == "SYSTEM_SETTING_UPDATE");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetSettingsAsync(new SystemSettingsQuery(Guid.NewGuid(), UserRole.Cashier)));
    }

    [Fact]
    public async Task Ef_store_supports_pos_checkout_with_sqlite()
    {
        await using var context = CreateContext(out _);
        var product = new Product
        {
            Name = "Chicken Breast",
            PricingType = PricingType.WeightBased,
            UnitOfMeasure = "Kg",
            SellingPrice = 1_000m,
            ReorderLevel = 5m,
            IsActive = true
        };
        var batch = new Batch
        {
            ProductId = product.ProductId,
            PurchaseDate = new DateTime(2026, 4, 29),
            InitialQuantity = 10m,
            RemainingQuantity = 10m,
            CostPrice = 600m,
            Status = BatchStatus.Active
        };
        await context.Products.AddAsync(product);
        await context.Batches.AddAsync(batch);
        await context.SaveChangesAsync();

        var dataStore = new EfCoreDataStore(context);
        var service = new POSCheckoutService(
            dataStore,
            new EfTransactionRunner(context),
            new EfAuditLogger(context),
            new FixedClock(new DateTimeOffset(2026, 4, 29, 9, 0, 0, TimeSpan.Zero)));

        var result = await service.CheckoutAsync(new CheckoutRequest(
            SaleChannel.Retail,
            CustomerId: null,
            CashierId: Guid.NewGuid(),
            PaymentMethod.Cash,
            [new CheckoutLineItem(product.ProductId, batch.BatchId, 2m, 1_000m, 0m)],
            CashAmount: 2_000m,
            CardAmount: 0m,
            CreditAmount: 0m,
            ManualDueDate: null));

        Assert.Equal("INV-20260429R-0001", result.InvoiceNumber);
        Assert.Equal(8m, batch.RemainingQuantity);
        Assert.Single(context.Invoices);
        Assert.Single(context.Payments);
        Assert.Contains(context.AuditLogs, log => log.Action == "INVOICE_CREATE");
    }

    [Fact]
    public async Task Offline_queue_persists_items_and_flush_marks_pending_items_processing()
    {
        await using var context = CreateContext(out _);
        var queue = new LocalOfflineSyncQueue(context);
        var entityId = Guid.NewGuid();

        await queue.EnqueueAsync(new OfflineSyncEnvelope("Invoice", entityId, "Upsert", "{\"ok\":true}", DateTimeOffset.UtcNow));
        var flushed = await queue.FlushAsync();

        var item = await context.OfflineSyncQueueItems.SingleAsync();
        Assert.Equal(1, flushed);
        Assert.Equal(entityId, item.EntityId);
        Assert.Equal(OfflineSyncStatus.Processing, item.Status);
        Assert.Equal(1, item.RetryCount);
    }

    [Fact]
    public async Task Database_management_applies_schema_reports_queue_status_and_creates_backup()
    {
        await using var context = CreateContext(out var databasePath);
        await context.OfflineSyncQueueItems.AddAsync(new OfflineSyncQueueItem
        {
            EntityName = "Notification",
            EntityId = Guid.NewGuid(),
            Operation = "Send",
            PayloadJson = "{}",
            Status = OfflineSyncStatus.Pending
        });
        await context.SaveChangesAsync();
        var options = CreateOptions(context.Database.GetConnectionString()!);
        var service = new DatabaseManagementService(context, options, new EfAuditLogger(context));
        var request = new AdminOperationRequest(Guid.NewGuid(), UserRole.Admin);

        var migrationResult = await service.ApplyMigrationsAsync(request);
        var status = await service.GetSyncQueueStatusAsync(request);
        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath)!, "backup");
        var backup = await service.BackupSqliteDatabaseAsync(new DatabaseBackupRequest(request.UserId, request.Role, backupDirectory));

        Assert.True(migrationResult.Succeeded);
        Assert.Equal(1, status.Pending);
        Assert.True(backup.Succeeded);
        Assert.True(File.Exists(backup.BackupPath));
    }

    [Fact]
    public async Task Receipt_pdf_contains_invoice_payment_and_return_data()
    {
        await using var context = CreateContext(out _);
        var invoice = SeedInvoiceForReports(context);
        invoice.RefundedAmount = 100m;
        await context.SalesReturns.AddAsync(new SalesReturn
        {
            InvoiceId = invoice.InvoiceId,
            ReturnDate = new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero),
            Reason = "Return",
            TotalValue = 100m,
            RefundAmount = 100m,
            ProcessedBy = Guid.NewGuid()
        });
        await context.Payments.AddAsync(new Payment
        {
            InvoiceId = invoice.InvoiceId,
            CustomerId = Guid.Empty,
            PaymentDate = invoice.InvoiceDate,
            Amount = 1_000m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentKind = PaymentKind.Payment,
            RecordedBy = invoice.CashierId
        });
        await context.SaveChangesAsync();
        var service = new ReceiptPdfService(context, CreateOptions(context.Database.GetConnectionString()!));

        var result = await service.GenerateInvoiceReceiptPdfAsync(new ReceiptPdfRequest(invoice.InvoiceId, Guid.NewGuid(), UserRole.Cashier));
        var pdfText = Encoding.ASCII.GetString(result.PdfBytes);

        Assert.Equal($"{invoice.InvoiceNumber}.pdf", result.FileName);
        Assert.StartsWith("%PDF", pdfText, StringComparison.Ordinal);
        Assert.Contains(invoice.InvoiceNumber, pdfText, StringComparison.Ordinal);
        Assert.Contains("Refunded: 100.00", pdfText, StringComparison.Ordinal);
        Assert.Contains("Return: 100.00 Refund 100.00", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operational_reporting_calculates_sales_profit_wastage_stock_returns_and_payments()
    {
        await using var context = CreateContext(out _);
        var invoice = SeedInvoiceForReports(context);
        await context.WastageRecords.AddAsync(new WastageRecord
        {
            BatchId = invoice.Items.Single().BatchId,
            ProductId = invoice.Items.Single().ProductId,
            WastageDate = new DateTime(2026, 4, 29),
            Quantity = 1m,
            WastageType = WastageType.CustomerReturn,
            EstimatedLoss = 250m,
            RecordedBy = Guid.NewGuid()
        });
        await context.SalesReturns.AddAsync(new SalesReturn
        {
            InvoiceId = invoice.InvoiceId,
            ReturnDate = new DateTimeOffset(2026, 4, 29, 12, 0, 0, TimeSpan.Zero),
            TotalValue = 200m,
            RefundAmount = 150m,
            Reason = "Partial",
            ProcessedBy = Guid.NewGuid()
        });
        await context.Payments.AddAsync(new Payment
        {
            InvoiceId = invoice.InvoiceId,
            CustomerId = Guid.Empty,
            PaymentDate = invoice.InvoiceDate,
            Amount = 1_000m,
            PaymentMethod = PaymentMethod.Cash,
            PaymentKind = PaymentKind.Payment,
            RecordedBy = invoice.CashierId
        });
        await context.AuditLogs.AddAsync(new AuditLog
        {
            UserId = invoice.CashierId,
            Role = UserRole.Cashier,
            Action = "POS_CHECKOUT",
            Module = "POS",
            TargetEntity = nameof(Invoice),
            TargetId = invoice.InvoiceId,
            Timestamp = invoice.InvoiceDate
        });
        await context.SaveChangesAsync();
        var service = new ReportingService(context);

        var report = await service.GenerateOperationalReportAsync(new OperationalReportRequest(
            new DateOnly(2026, 4, 29),
            new DateOnly(2026, 4, 29),
            Guid.NewGuid(),
            UserRole.Admin));

        Assert.Equal(1_000m, report.Sales.TotalSales);
        Assert.Equal(400m, report.Profit.GrossProfit);
        Assert.Equal(250m, report.Wastage.TotalWastageValue);
        Assert.Equal(1, report.Wastage.RecordCount);
        Assert.Equal(1_000m, report.Payments.TotalCash);
        Assert.Equal(200m, report.SalesReturns.TotalReturnValue);
        Assert.Single(report.StockOnHand);
        Assert.Single(report.BatchMovements);
        Assert.Single(report.AuditActivity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateOperationalReportAsync(new OperationalReportRequest(
            new DateOnly(2026, 4, 29),
            new DateOnly(2026, 4, 29),
            Guid.NewGuid(),
            UserRole.Cashier)));
    }

    private static PoultryProDbContext CreateContext(out string databasePath)
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"bluebirds-tests-{Guid.NewGuid():N}.sqlite3");
        var options = new DbContextOptionsBuilder<PoultryProDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        var context = new PoultryProDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static InfrastructureOptions CreateOptions(
        string connectionString,
        string environmentName = "Test",
        SecurityOptions? security = null,
        RecoveryOptions? recovery = null,
        DevelopmentBootstrapOptions? developmentBootstrap = null)
    {
        return new InfrastructureOptions
        {
            EnvironmentName = environmentName,
            Database = new DatabaseOptions
            {
                Provider = "SQLite",
                LocalPosConnectionString = connectionString,
                BackupDirectory = Path.Combine(Path.GetTempPath(), "bluebirds-test-backups")
            },
            Security = security ?? new SecurityOptions(),
            Recovery = recovery ?? new RecoveryOptions(),
            DevelopmentBootstrap = developmentBootstrap ?? new DevelopmentBootstrapOptions()
        };
    }

    private static Invoice SeedInvoiceForReports(PoultryProDbContext context)
    {
        var product = new Product
        {
            Name = "Chicken Breast",
            PricingType = PricingType.WeightBased,
            UnitOfMeasure = "Kg",
            SellingPrice = 500m,
            ReorderLevel = 5m,
            IsActive = true
        };
        var batch = new Batch
        {
            ProductId = product.ProductId,
            PurchaseDate = new DateTime(2026, 4, 20),
            InitialQuantity = 10m,
            RemainingQuantity = 8m,
            CostPrice = 300m,
            Status = BatchStatus.Active
        };
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-20260429R-0001",
            CashierId = Guid.NewGuid(),
            SaleChannel = SaleChannel.Retail,
            PaymentMethod = PaymentMethod.Cash,
            InvoiceDate = new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
            Subtotal = 1_000m,
            GrandTotal = 1_000m,
            PaidAmount = 1_000m,
            PaymentStatus = PaymentStatus.Paid
        };
        invoice.Items.Add(new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = product.ProductId,
            BatchId = batch.BatchId,
            ProductName = product.Name,
            BatchReference = "BATCH-1",
            UnitOfMeasure = "Kg",
            Quantity = 2m,
            UnitPrice = 500m,
            LineTotal = 1_000m
        });
        context.Products.Add(product);
        context.Batches.Add(batch);
        context.Invoices.Add(invoice);
        return invoice;
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"HASH:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"HASH:{password}";
    }

    private sealed class TestPasswordGenerator(string password) : ITemporaryPasswordGenerator
    {
        public string Generate() => password;
    }

    private sealed class FixedClock(DateTimeOffset? now = null) : ISystemClock
    {
        public DateTimeOffset Now { get; } = now ?? new DateTimeOffset(2026, 4, 29, 11, 0, 0, TimeSpan.Zero);
    }
}
