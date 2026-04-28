using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class POSCheckoutServiceTests
{
    [Fact]
    public void Retail_payment_options_hide_credit_and_mixed_methods()
    {
        var service = CreateHarness().Service;

        var methods = service.GetAllowedPaymentMethods(SaleChannel.Retail);

        Assert.Equal([PaymentMethod.Cash, PaymentMethod.Card], methods);
    }

    [Fact]
    public void Wholesale_payment_options_include_cash_card_credit_and_mixed()
    {
        var service = CreateHarness().Service;

        var methods = service.GetAllowedPaymentMethods(SaleChannel.Wholesale);

        Assert.Equal([PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.Credit, PaymentMethod.Mixed], methods);
    }

    [Fact]
    public async Task Batch_picker_returns_available_batches_with_required_display_fields()
    {
        var harness = CreateHarness();
        var olderBatch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 10m, purchaseDate: new DateTime(2026, 4, 1), expiryDate: new DateTime(2026, 4, 5));
        var newerBatch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 5m, purchaseDate: new DateTime(2026, 4, 2), expiryDate: new DateTime(2026, 4, 6));

        var options = await harness.Service.GetBatchPickerOptionsAsync(harness.WeightProduct.ProductId);

        Assert.Equal([olderBatch.BatchId, newerBatch.BatchId], options.Select(option => option.BatchId));
        Assert.Equal(olderBatch.PurchaseDate, options[0].PurchaseDate);
        Assert.Equal(olderBatch.ExpiryDate, options[0].ExpiryDate);
        Assert.Equal(olderBatch.RemainingQuantity, options[0].RemainingQuantity);
        Assert.False(string.IsNullOrWhiteSpace(options[0].BatchReference));
    }

    [Fact]
    public async Task Wholesale_mixed_checkout_creates_itemized_invoice_and_deducts_selected_batches()
    {
        var harness = CreateHarness();
        var customer = harness.AddBusinessCustomer(creditLimit: 5_000m, creditPeriodDays: 10, outstanding: 1_000m);
        var chickenBatch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 20m);
        var eggBatch = harness.AddBatch(harness.UnitProduct.ProductId, remainingQuantity: 12m);

        var result = await harness.Service.CheckoutAsync(new CheckoutRequest(
            SaleChannel.Wholesale,
            customer.CustomerId,
            harness.CashierId,
            PaymentMethod.Mixed,
            [
                new CheckoutLineItem(harness.WeightProduct.ProductId, chickenBatch.BatchId, 2.5m, 999m, 100m),
                new CheckoutLineItem(harness.UnitProduct.ProductId, eggBatch.BatchId, 3m, 999m, 0m)
            ],
            CashAmount: 1_000m,
            CardAmount: 500m,
            CreditAmount: 1_650m,
            ManualDueDate: null));

        Assert.Equal("INV-20260428W-0001", result.InvoiceNumber);
        Assert.Equal(3_150m, result.GrandTotal);
        Assert.Equal(1_500m, result.PaidAmount);
        Assert.Equal(1_650m, result.BalanceAmount);
        Assert.Equal(PaymentStatus.Partial, result.PaymentStatus);
        Assert.Equal(new DateTime(2026, 5, 8), result.DueDate);
        Assert.Equal(17.5m, chickenBatch.RemainingQuantity);
        Assert.Equal(9m, eggBatch.RemainingQuantity);
        Assert.Equal(2, harness.Store.Payments.Count);
        Assert.Equal([PaymentMethod.Cash, PaymentMethod.Card], harness.Store.Payments.Select(payment => payment.PaymentMethod));
        Assert.Equal("Chicken Breast", result.Receipt.Lines.First().ProductName);
        Assert.Equal("kg", result.Receipt.Lines.First().UnitOfMeasure);
        Assert.Equal(1_000m, result.Receipt.Lines.First().UnitPrice);
        Assert.Equal(3_150m, result.Receipt.GrandTotal);
        Assert.Equal(2_650m, result.CreditPanel?.OutstandingBalance);
        Assert.False(result.CreditPanel?.IsLimitBlocking);
    }

    [Fact]
    public async Task One_time_creditor_credit_invoice_requires_manual_due_date()
    {
        var harness = CreateHarness();
        var customer = harness.AddOneTimeCreditor();
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 20m);

        var request = new CheckoutRequest(
            SaleChannel.Wholesale,
            customer.CustomerId,
            harness.CashierId,
            PaymentMethod.Credit,
            [new CheckoutLineItem(harness.WeightProduct.ProductId, batch.BatchId, 1m, 1_000m, 0m)],
            CashAmount: 0m,
            CardAmount: 0m,
            CreditAmount: 1_000m,
            ManualDueDate: null);

        await Assert.ThrowsAsync<POSValidationException>(() => harness.Service.CheckoutAsync(request));
    }

    [Fact]
    public async Task One_time_creditor_credit_invoice_uses_manual_due_date()
    {
        var harness = CreateHarness();
        var customer = harness.AddOneTimeCreditor();
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 20m);

        var result = await harness.Service.CheckoutAsync(new CheckoutRequest(
            SaleChannel.Wholesale,
            customer.CustomerId,
            harness.CashierId,
            PaymentMethod.Credit,
            [new CheckoutLineItem(harness.WeightProduct.ProductId, batch.BatchId, 1m, 1_000m, 0m)],
            CashAmount: 0m,
            CardAmount: 0m,
            CreditAmount: 1_000m,
            ManualDueDate: new DateTime(2026, 5, 12)));

        Assert.Equal(new DateTime(2026, 5, 12), result.DueDate);
        Assert.Equal(PaymentStatus.Pending, result.PaymentStatus);
    }

    [Fact]
    public async Task Retail_checkout_rejects_credit_payment()
    {
        var harness = CreateHarness();
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 20m);

        var request = new CheckoutRequest(
            SaleChannel.Retail,
            CustomerId: null,
            harness.CashierId,
            PaymentMethod.Credit,
            [new CheckoutLineItem(harness.WeightProduct.ProductId, batch.BatchId, 1m, 1_000m, 0m)],
            CashAmount: 0m,
            CardAmount: 0m,
            CreditAmount: 1_000m,
            ManualDueDate: null);

        await Assert.ThrowsAsync<POSValidationException>(() => harness.Service.CheckoutAsync(request));
    }

    [Fact]
    public async Task Aggregated_batch_stock_cannot_go_negative_and_no_deduction_is_applied()
    {
        var harness = CreateHarness();
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 3m);

        var request = new CheckoutRequest(
            SaleChannel.Retail,
            CustomerId: null,
            harness.CashierId,
            PaymentMethod.Cash,
            [
                new CheckoutLineItem(harness.WeightProduct.ProductId, batch.BatchId, 2m, 1_000m, 0m),
                new CheckoutLineItem(harness.WeightProduct.ProductId, batch.BatchId, 2m, 1_000m, 0m)
            ],
            CashAmount: 4_000m,
            CardAmount: 0m,
            CreditAmount: 0m,
            ManualDueDate: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.CheckoutAsync(request));

        Assert.Equal(3m, batch.RemainingQuantity);
        Assert.Empty(harness.Store.Invoices);
    }

    [Fact]
    public async Task Credit_limit_alert_is_non_blocking_when_business_account_exceeds_limit()
    {
        var harness = CreateHarness();
        var customer = harness.AddBusinessCustomer(creditLimit: 1_000m, creditPeriodDays: 5, outstanding: 900m);
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 20m);

        var result = await harness.Service.CheckoutAsync(new CheckoutRequest(
            SaleChannel.Wholesale,
            customer.CustomerId,
            harness.CashierId,
            PaymentMethod.Credit,
            [new CheckoutLineItem(harness.WeightProduct.ProductId, batch.BatchId, 1m, 1_000m, 0m)],
            CashAmount: 0m,
            CardAmount: 0m,
            CreditAmount: 1_000m,
            ManualDueDate: null));

        Assert.True(result.CreditPanel?.IsLimitAlertActive);
        Assert.False(result.CreditPanel?.IsLimitBlocking);
        Assert.Equal(-900m, result.CreditPanel?.AvailableCredit);
        Assert.Single(harness.Store.Invoices);
    }

    [Fact]
    public async Task Admin_can_void_invoice_with_reason_and_audit_entry()
    {
        var harness = CreateHarness();
        var invoice = harness.Store.AddExistingInvoice("INV-20260428R-0007");

        var result = await harness.Service.VoidInvoiceAsync(new VoidInvoiceRequest(
            invoice.InvoiceId,
            harness.AdminId,
            UserRole.Admin,
            "Incorrect product selected"));

        Assert.Equal(PaymentStatus.Void, result.PaymentStatus);
        Assert.Equal("Incorrect product selected", invoice.VoidReason);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("INVOICE_VOID", harness.AuditLogger.Entries[0].Action);
    }

    [Fact]
    public async Task Cashier_cannot_void_invoice()
    {
        var harness = CreateHarness();
        var invoice = harness.Store.AddExistingInvoice("INV-20260428R-0007");

        await Assert.ThrowsAsync<POSValidationException>(() => harness.Service.VoidInvoiceAsync(new VoidInvoiceRequest(
            invoice.InvoiceId,
            harness.CashierId,
            UserRole.Cashier,
            "Incorrect product selected")));
    }

    [Fact]
    public async Task Sales_return_voids_invoice_refunds_paid_amount_and_records_wastage_loss_without_restocking()
    {
        var harness = CreateHarness();
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 8m);
        var invoice = harness.Store.AddExistingInvoice(
            "INV-20260428R-0008",
            harness.CashierId,
            SaleChannel.Retail,
            PaymentMethod.Cash,
            paidAmount: 2_000m,
            balanceAmount: 0m);
        var invoiceItem = new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = harness.WeightProduct.ProductId,
            BatchId = batch.BatchId,
            ProductName = harness.WeightProduct.Name,
            BatchReference = "BATCH001",
            UnitOfMeasure = "kg",
            Quantity = 2m,
            UnitPrice = 1_000m,
            DiscountAmount = 0m,
            LineTotal = 2_000m
        };
        invoice.Items.Add(invoiceItem);

        var result = await harness.Service.ProcessSalesReturnAsync(new SalesReturnRequest(
            invoice.InvoiceId,
            harness.CashierId,
            UserRole.Cashier,
            "Customer returned damaged package",
            PaymentMethod.Cash,
            [new SalesReturnLine(invoiceItem.ItemId, 2m)]));

        Assert.Equal(PaymentStatus.Void, result.PaymentStatus);
        Assert.True(result.IsFullReturn);
        Assert.Equal(2_000m, result.ReturnValue);
        Assert.Equal(2_000m, result.RefundAmount);
        Assert.Equal(0m, result.BalanceReduction);
        Assert.Equal(0m, result.RemainingInvoiceBalance);
        Assert.Equal(2_000m, invoice.RefundedAmount);
        Assert.Equal(0m, invoice.BalanceAmount);
        Assert.Equal("Customer returned damaged package", invoice.VoidReason);
        Assert.Equal(8m, batch.RemainingQuantity);
        Assert.Single(harness.Store.SalesReturns);
        Assert.Equal(2_000m, harness.Store.SalesReturns[0].RefundAmount);
        Assert.Single(harness.Store.WastageRecords);
        Assert.Equal(WastageType.CustomerReturn, harness.Store.WastageRecords[0].WastageType);
        Assert.Equal(1_600m, harness.Store.WastageRecords[0].EstimatedLoss);
        Assert.Single(harness.Store.Payments.Where(payment => payment.PaymentKind == PaymentKind.Refund));
        Assert.Equal(["SALES_RETURN_PROCESS", "INVOICE_VOID"], harness.AuditLogger.Entries.Select(entry => entry.Action));
    }

    [Fact]
    public async Task Partial_return_on_paid_invoice_keeps_invoice_active_and_refunds_return_value_at_sold_rate()
    {
        var harness = CreateHarness();
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 8m);
        var invoice = harness.Store.AddExistingInvoice(
            "INV-20260428R-0009",
            harness.CashierId,
            SaleChannel.Retail,
            PaymentMethod.Cash,
            paidAmount: 2_000m,
            balanceAmount: 0m);
        var invoiceItem = new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = harness.WeightProduct.ProductId,
            BatchId = batch.BatchId,
            ProductName = harness.WeightProduct.Name,
            BatchReference = "BATCH001",
            UnitOfMeasure = "kg",
            Quantity = 2m,
            UnitPrice = 1_000m,
            DiscountAmount = 200m,
            LineTotal = 1_800m
        };
        invoice.Items.Add(invoiceItem);
        invoice.GrandTotal = 1_800m;
        invoice.Subtotal = 2_000m;
        invoice.DiscountTotal = 200m;
        invoice.PaidAmount = 1_800m;

        var result = await harness.Service.ProcessSalesReturnAsync(new SalesReturnRequest(
            invoice.InvoiceId,
            harness.CashierId,
            UserRole.Cashier,
            "Partial return",
            PaymentMethod.Cash,
            [new SalesReturnLine(invoiceItem.ItemId, 1m)]));

        Assert.False(result.IsFullReturn);
        Assert.Equal(PaymentStatus.Paid, result.PaymentStatus);
        Assert.Equal(900m, result.ReturnValue);
        Assert.Equal(900m, result.RefundAmount);
        Assert.Equal(0m, result.BalanceReduction);
        Assert.Equal(0m, result.RemainingInvoiceBalance);
        Assert.Equal(900m, invoice.GrandTotal);
        Assert.Equal(900m, invoice.RefundedAmount);
        Assert.Null(invoice.VoidReason);
        Assert.Single(harness.Store.Payments.Where(payment => payment.PaymentKind == PaymentKind.Refund));
        Assert.Equal(["SALES_RETURN_PROCESS"], harness.AuditLogger.Entries.Select(entry => entry.Action));
        Assert.Equal(8m, batch.RemainingQuantity);
        Assert.Single(harness.Store.WastageRecords);
    }

    [Fact]
    public async Task Partial_return_on_mixed_invoice_reduces_credit_balance_first_then_refunds_excess_paid_portion()
    {
        var harness = CreateHarness();
        var customer = harness.AddBusinessCustomer(creditLimit: 5_000m, creditPeriodDays: 10, outstanding: 500m);
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 8m);
        var invoice = harness.Store.AddExistingInvoice(
            "INV-20260428W-0004",
            harness.CashierId,
            SaleChannel.Wholesale,
            PaymentMethod.Mixed,
            paidAmount: 1_500m,
            balanceAmount: 500m,
            customerId: customer.CustomerId);
        var invoiceItem = new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = harness.WeightProduct.ProductId,
            BatchId = batch.BatchId,
            ProductName = harness.WeightProduct.Name,
            BatchReference = "BATCH001",
            UnitOfMeasure = "kg",
            Quantity = 2m,
            UnitPrice = 1_000m,
            DiscountAmount = 0m,
            LineTotal = 2_000m
        };
        invoice.Items.Add(invoiceItem);

        var result = await harness.Service.ProcessSalesReturnAsync(new SalesReturnRequest(
            invoice.InvoiceId,
            harness.CashierId,
            UserRole.Cashier,
            "Partial mixed return",
            PaymentMethod.Card,
            [new SalesReturnLine(invoiceItem.ItemId, 1m)]));

        Assert.False(result.IsFullReturn);
        Assert.Equal(1_000m, result.ReturnValue);
        Assert.Equal(500m, result.BalanceReduction);
        Assert.Equal(500m, result.RefundAmount);
        Assert.Equal(0m, result.RemainingInvoiceBalance);
        Assert.Equal(0m, invoice.BalanceAmount);
        Assert.Equal(500m, invoice.RefundedAmount);
        Assert.Equal(0m, harness.Store.BusinessAccounts[customer.CustomerId].OutstandingBalance);
        Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
    }

    [Fact]
    public async Task Sales_return_clears_credit_balance_for_business_account()
    {
        var harness = CreateHarness();
        var customer = harness.AddBusinessCustomer(creditLimit: 5_000m, creditPeriodDays: 10, outstanding: 1_650m);
        var batch = harness.AddBatch(harness.WeightProduct.ProductId, remainingQuantity: 8m);
        var invoice = harness.Store.AddExistingInvoice(
            "INV-20260428W-0003",
            harness.CashierId,
            SaleChannel.Wholesale,
            PaymentMethod.Mixed,
            paidAmount: 1_500m,
            balanceAmount: 1_650m,
            customerId: customer.CustomerId);
        invoice.GrandTotal = 3_150m;
        invoice.Items.Add(new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            ProductId = harness.WeightProduct.ProductId,
            BatchId = batch.BatchId,
            ProductName = harness.WeightProduct.Name,
            BatchReference = "BATCH001",
            UnitOfMeasure = "kg",
            Quantity = 3.15m,
            UnitPrice = 1_000m,
            DiscountAmount = 0m,
            LineTotal = 3_150m
        });

        await harness.Service.ProcessSalesReturnAsync(new SalesReturnRequest(
            invoice.InvoiceId,
            harness.CashierId,
            UserRole.Cashier,
            "Returned wholesale order",
            PaymentMethod.Card,
            [new SalesReturnLine(invoice.Items.Single().ItemId, 3.15m)]));

        Assert.Equal(0m, harness.Store.BusinessAccounts[customer.CustomerId].OutstandingBalance);
        Assert.Equal(1_500m, invoice.RefundedAmount);
        Assert.Equal(0m, invoice.BalanceAmount);
    }

    private static TestHarness CreateHarness()
    {
        return new TestHarness();
    }

    private sealed class TestHarness
    {
        public Guid CashierId { get; } = Guid.NewGuid();
        public Guid AdminId { get; } = Guid.NewGuid();
        public InMemoryPOSDataStore Store { get; } = new();
        public RecordingAuditLogger AuditLogger { get; } = new();
        public POSCheckoutService Service { get; }
        public Product WeightProduct { get; }
        public Product UnitProduct { get; }

        public TestHarness()
        {
            WeightProduct = Store.AddProduct("Chicken Breast", PricingType.WeightBased, "kg", 1_000m);
            UnitProduct = Store.AddProduct("Egg Tray", PricingType.UnitBased, "tray", 250m);
            Service = new POSCheckoutService(
                Store,
                new InlineTransactionRunner(),
                AuditLogger,
                new FixedClock(new DateTimeOffset(2026, 4, 28, 10, 30, 0, TimeSpan.Zero)));
        }

        public Batch AddBatch(Guid productId, decimal remainingQuantity, DateTime? purchaseDate = null, DateTime? expiryDate = null)
        {
            return Store.AddBatch(productId, remainingQuantity, purchaseDate, expiryDate);
        }

        public Customer AddBusinessCustomer(decimal creditLimit, int creditPeriodDays, decimal outstanding)
        {
            var customer = Store.AddCustomer(AccountType.BusinessAccount);
            Store.BusinessAccounts[customer.CustomerId] = new BusinessAccount
            {
                CustomerId = customer.CustomerId,
                CreditLimit = creditLimit,
                CreditPeriodDays = creditPeriodDays,
                NotificationLeadDays = 3,
                OutstandingBalance = outstanding
            };
            Store.CreditSnapshots[customer.CustomerId] = new CreditPanelSnapshot(
                customer.CustomerId,
                outstanding,
                creditLimit,
                OverdueInvoiceCount: 2,
                LastPaymentDate: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero));

            return customer;
        }

        public Customer AddOneTimeCreditor()
        {
            var customer = Store.AddCustomer(AccountType.OneTimeCreditor);
            Store.CreditSnapshots[customer.CustomerId] = new CreditPanelSnapshot(
                customer.CustomerId,
                OutstandingBalance: 0m,
                CreditLimit: null,
                OverdueInvoiceCount: 0,
                LastPaymentDate: null);

            return customer;
        }
    }

    private sealed class InMemoryPOSDataStore : IPOSDataStore
    {
        private readonly Dictionary<Guid, Product> _products = [];
        private readonly Dictionary<Guid, Batch> _batches = [];
        private readonly Dictionary<Guid, Customer> _customers = [];
        private readonly Dictionary<(DateOnly Date, SaleChannel Channel), int> _sequences = [];

        public Dictionary<Guid, BusinessAccount> BusinessAccounts { get; } = [];
        public Dictionary<Guid, CreditPanelSnapshot> CreditSnapshots { get; } = [];
        public List<Invoice> Invoices { get; } = [];
        public List<Payment> Payments { get; } = [];
        public List<SalesReturn> SalesReturns { get; } = [];
        public List<WastageRecord> WastageRecords { get; } = [];

        public Product AddProduct(string name, PricingType pricingType, string unit, decimal price)
        {
            var product = new Product
            {
                Name = name,
                PricingType = pricingType,
                UnitOfMeasure = unit,
                SellingPrice = price,
                IsActive = true
            };

            _products[product.ProductId] = product;
            return product;
        }

        public Batch AddBatch(Guid productId, decimal remainingQuantity, DateTime? purchaseDate, DateTime? expiryDate)
        {
            var batch = new Batch
            {
                ProductId = productId,
                PurchaseDate = purchaseDate ?? new DateTime(2026, 4, 1),
                ExpiryDate = expiryDate,
                InitialQuantity = remainingQuantity,
                RemainingQuantity = remainingQuantity,
                CostPrice = 800m,
                Status = BatchStatus.Active
            };

            _batches[batch.BatchId] = batch;
            return batch;
        }

        public Customer AddCustomer(AccountType accountType)
        {
            var customer = new Customer
            {
                Name = "Blue Birds Customer",
                Phone = "0110000000",
                WhatsAppNo = "+94770000000",
                CustomerType = CustomerType.Wholesale,
                AccountType = accountType
            };

            _customers[customer.CustomerId] = customer;
            return customer;
        }

        public Invoice AddExistingInvoice(string invoiceNumber)
        {
            return AddExistingInvoice(
                invoiceNumber,
                cashierId: Guid.NewGuid(),
                SaleChannel.Retail,
                PaymentMethod.Cash,
                paidAmount: 0m,
                balanceAmount: 0m);
        }

        public Invoice AddExistingInvoice(
            string invoiceNumber,
            Guid cashierId,
            SaleChannel saleChannel,
            PaymentMethod paymentMethod,
            decimal paidAmount,
            decimal balanceAmount,
            Guid? customerId = null)
        {
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                CustomerId = customerId,
                CashierId = cashierId,
                SaleChannel = saleChannel,
                PaymentMethod = paymentMethod,
                GrandTotal = paidAmount + balanceAmount,
                PaidAmount = paidAmount,
                BalanceAmount = balanceAmount,
                PaymentStatus = balanceAmount == 0m ? PaymentStatus.Paid : PaymentStatus.Partial
            };

            Invoices.Add(invoice);
            return invoice;
        }

        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            _products.TryGetValue(productId, out var product);
            return Task.FromResult(product);
        }

        public Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            _batches.TryGetValue(batchId, out var batch);
            return Task.FromResult(batch);
        }

        public Task<IReadOnlyList<Batch>> GetAvailableBatchesAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Batch> batches = _batches.Values
                .Where(batch => batch.ProductId == productId && batch.Status == BatchStatus.Active && batch.RemainingQuantity > 0)
                .ToList();

            return Task.FromResult(batches);
        }

        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            _customers.TryGetValue(customerId, out var customer);
            return Task.FromResult(customer);
        }

        public Task<BusinessAccount?> GetBusinessAccountAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            BusinessAccounts.TryGetValue(customerId, out var account);
            return Task.FromResult(account);
        }

        public Task<CreditPanelSnapshot?> GetCreditPanelSnapshotAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            CreditSnapshots.TryGetValue(customerId, out var snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<int> GetNextInvoiceSequenceAsync(DateOnly invoiceDate, SaleChannel saleChannel, CancellationToken cancellationToken = default)
        {
            var key = (invoiceDate, saleChannel);
            _sequences.TryGetValue(key, out var current);
            var next = current + 1;
            _sequences[key] = next;
            return Task.FromResult(next);
        }

        public Task AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            Invoices.Add(invoice);
            return Task.CompletedTask;
        }

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default)
        {
            SalesReturns.Add(salesReturn);
            return Task.CompletedTask;
        }

        public Task AddWastageRecordAsync(WastageRecord wastageRecord, CancellationToken cancellationToken = default)
        {
            WastageRecords.Add(wastageRecord);
            return Task.CompletedTask;
        }

        public Task<decimal> GetPreviouslyReturnedQuantityAsync(Guid invoiceItemId, CancellationToken cancellationToken = default)
        {
            var quantity = SalesReturns
                .SelectMany(salesReturn => salesReturn.Items)
                .Where(item => item.InvoiceItemId == invoiceItemId)
                .Sum(item => item.Quantity);

            return Task.FromResult(quantity);
        }

        public Task UpdateBatchAsync(Batch batch, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default)
        {
            CreditSnapshots[account.CustomerId] = new CreditPanelSnapshot(
                account.CustomerId,
                account.OutstandingBalance,
                account.CreditLimit,
                CreditSnapshots.TryGetValue(account.CustomerId, out var current) ? current.OverdueInvoiceCount : 0,
                CreditSnapshots.TryGetValue(account.CustomerId, out current) ? current.LastPaymentDate : null);
            return Task.CompletedTask;
        }

        public Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Invoices.SingleOrDefault(invoice => invoice.InvoiceId == invoiceId));
        }

        public Task UpdateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
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
