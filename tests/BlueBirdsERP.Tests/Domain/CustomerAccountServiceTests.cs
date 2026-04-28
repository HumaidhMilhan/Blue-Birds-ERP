using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.CustomerAccounts;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class CustomerAccountServiceTests
{
    [Fact]
    public async Task Admin_can_create_business_account_with_configurable_terms()
    {
        var harness = CreateHarness();

        var result = await harness.Service.CreateBusinessAccountAsync(new CreateBusinessAccountRequest(
            "ABC Hotels",
            "0112223333",
            "+94771112222",
            "ap@abc.test",
            "Colombo",
            CreditLimit: 250_000m,
            CreditPeriodDays: 14,
            NotificationLeadDays: 3,
            harness.AdminId,
            UserRole.Admin));

        var account = harness.Store.BusinessAccounts.Values.Single();
        Assert.Equal(AccountType.BusinessAccount, result.AccountType);
        Assert.Equal(250_000m, result.CreditLimit);
        Assert.Equal(14, account.CreditPeriodDays);
        Assert.Equal(3, account.NotificationLeadDays);
        Assert.Equal(0m, account.OutstandingBalance);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("BUSINESS_ACCOUNT_CREATE", harness.AuditLogger.Entries[0].Action);
    }

    [Fact]
    public async Task Admin_can_create_one_time_creditor_without_credit_terms()
    {
        var harness = CreateHarness();

        var result = await harness.Service.CreateOneTimeCreditorAsync(new CreateOneTimeCreditorRequest(
            "Kamal Perera",
            "0771234567",
            "+94771234567",
            "Gampaha",
            "991234567V",
            harness.AdminId,
            UserRole.Admin));

        var customer = harness.Store.Customers.Values.Single();
        Assert.Equal(AccountType.OneTimeCreditor, result.AccountType);
        Assert.Null(result.CreditLimit);
        Assert.Null(result.AvailableCredit);
        Assert.Equal("991234567V", customer.NicOrBusinessRegistrationNumber);
        Assert.Empty(harness.Store.BusinessAccounts);
    }

    [Fact]
    public async Task General_payment_for_one_time_creditor_is_distributed_across_oldest_invoices()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer(AccountType.OneTimeCreditor);
        var oldInvoice = harness.Store.AddInvoice(customer.CustomerId, "INV-20260401W-0001", new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 10), balance: 1_000m);
        var newerInvoice = harness.Store.AddInvoice(customer.CustomerId, "INV-20260405W-0001", new DateTimeOffset(2026, 4, 5, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 12), balance: 800m);

        var result = await harness.Service.RecordAccountPaymentAsync(new AccountPaymentRequest(
            customer.CustomerId,
            InvoiceId: null,
            Amount: 1_200m,
            PaymentMethod.Cash,
            Reference: "RCPT-001",
            harness.CashierId,
            UserRole.Cashier));

        Assert.Equal(2, result.Allocations.Count);
        Assert.Equal(oldInvoice.InvoiceId, result.Allocations[0].InvoiceId);
        Assert.Equal(1_000m, result.Allocations[0].AmountApplied);
        Assert.Equal(PaymentStatus.Paid, oldInvoice.PaymentStatus);
        Assert.Equal(600m, newerInvoice.BalanceAmount);
        Assert.Equal(PaymentStatus.Partial, newerInvoice.PaymentStatus);
        Assert.Equal(600m, result.RemainingOutstanding);
        Assert.Equal(2, harness.Store.Payments.Count);
    }

    [Fact]
    public async Task Overpayment_is_rejected_for_general_account_payment()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer(AccountType.OneTimeCreditor);
        harness.Store.AddInvoice(customer.CustomerId, "INV-20260401W-0001", new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 10), balance: 1_000m);

        await Assert.ThrowsAsync<CustomerAccountValidationException>(() => harness.Service.RecordAccountPaymentAsync(new AccountPaymentRequest(
            customer.CustomerId,
            InvoiceId: null,
            Amount: 1_001m,
            PaymentMethod.Cash,
            Reference: null,
            harness.CashierId,
            UserRole.Cashier)));
    }

    [Fact]
    public async Task Payment_against_business_account_invoice_updates_outstanding_and_available_credit()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer(AccountType.BusinessAccount);
        harness.Store.BusinessAccounts[customer.CustomerId] = new BusinessAccount
        {
            CustomerId = customer.CustomerId,
            CreditLimit = 5_000m,
            CreditPeriodDays = 7,
            NotificationLeadDays = 2,
            OutstandingBalance = 1_500m
        };
        var invoice = harness.Store.AddInvoice(customer.CustomerId, "INV-20260401W-0001", new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 8), balance: 1_500m);

        var result = await harness.Service.RecordAccountPaymentAsync(new AccountPaymentRequest(
            customer.CustomerId,
            invoice.InvoiceId,
            Amount: 500m,
            PaymentMethod.Card,
            Reference: "CARD-22",
            harness.CashierId,
            UserRole.Cashier));
        var summary = await harness.Service.GetCreditSummaryAsync(customer.CustomerId);

        Assert.Single(result.Allocations);
        Assert.Equal(1_000m, invoice.BalanceAmount);
        Assert.Equal(1_000m, harness.Store.BusinessAccounts[customer.CustomerId].OutstandingBalance);
        Assert.Equal(4_000m, summary.AvailableCredit);
        Assert.False(summary.IsLimitAlertActive);
    }

    [Fact]
    public async Task Admin_editing_business_terms_recalculates_existing_unpaid_invoice_due_dates_and_audits()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer(AccountType.BusinessAccount);
        harness.Store.BusinessAccounts[customer.CustomerId] = new BusinessAccount
        {
            CustomerId = customer.CustomerId,
            CreditLimit = 2_000m,
            CreditPeriodDays = 7,
            NotificationLeadDays = 2,
            OutstandingBalance = 1_000m
        };
        var unpaidInvoice = harness.Store.AddInvoice(customer.CustomerId, "INV-20260401W-0001", new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 8), balance: 1_000m);
        var paidInvoice = harness.Store.AddInvoice(customer.CustomerId, "INV-20260402W-0001", new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 9), balance: 0m);
        paidInvoice.PaymentStatus = PaymentStatus.Paid;

        await harness.Service.UpdateBusinessAccountTermsAsync(new UpdateBusinessAccountTermsRequest(
            customer.CustomerId,
            CreditLimit: 3_000m,
            CreditPeriodDays: 21,
            NotificationLeadDays: 5,
            harness.AdminId,
            UserRole.Admin));

        Assert.Equal(new DateTime(2026, 4, 22), unpaidInvoice.DueDate);
        Assert.Equal(new DateTime(2026, 4, 9), paidInvoice.DueDate);
        Assert.Equal(3_000m, harness.Store.BusinessAccounts[customer.CustomerId].CreditLimit);
        Assert.Equal(21, harness.Store.BusinessAccounts[customer.CustomerId].CreditPeriodDays);
        Assert.Single(harness.AuditLogger.Entries);
        Assert.Equal("BUSINESS_ACCOUNT_TERMS_EDIT", harness.AuditLogger.Entries[0].Action);
    }

    [Fact]
    public async Task Debtor_aging_report_groups_outstanding_balances_by_due_date()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer(AccountType.BusinessAccount);
        harness.Store.BusinessAccounts[customer.CustomerId] = new BusinessAccount
        {
            CustomerId = customer.CustomerId,
            CreditLimit = 10_000m,
            CreditPeriodDays = 7,
            NotificationLeadDays = 2,
            OutstandingBalance = 8_000m
        };
        harness.Store.AddInvoice(customer.CustomerId, "CURRENT", new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 28), balance: 100m);
        harness.Store.AddInvoice(customer.CustomerId, "D30", new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 1), balance: 200m);
        harness.Store.AddInvoice(customer.CustomerId, "D60", new DateTimeOffset(2026, 2, 20, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 3, 1), balance: 300m);
        harness.Store.AddInvoice(customer.CustomerId, "D90", new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 2, 1), balance: 400m);
        harness.Store.AddInvoice(customer.CustomerId, "D90PLUS", new DateTimeOffset(2025, 12, 20, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 1, 1), balance: 500m);

        var report = await harness.Service.GenerateDebtorAgingReportAsync(new DateOnly(2026, 4, 28));

        Assert.Equal(100m, report.Buckets.Single(bucket => bucket.Name == "Current").OutstandingBalance);
        Assert.Equal(200m, report.Buckets.Single(bucket => bucket.Name == "1-30 days overdue").OutstandingBalance);
        Assert.Equal(300m, report.Buckets.Single(bucket => bucket.Name == "31-60 days overdue").OutstandingBalance);
        Assert.Equal(400m, report.Buckets.Single(bucket => bucket.Name == "61-90 days overdue").OutstandingBalance);
        Assert.Equal(500m, report.Buckets.Single(bucket => bucket.Name == "90+ days overdue").OutstandingBalance);
    }

    [Fact]
    public async Task Payment_history_returns_invoice_number_date_amount_and_status()
    {
        var harness = CreateHarness();
        var customer = harness.Store.AddCustomer(AccountType.OneTimeCreditor);
        var invoice = harness.Store.AddInvoice(customer.CustomerId, "INV-20260401W-0001", new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero), dueDate: new DateTime(2026, 4, 10), balance: 1_000m);
        await harness.Service.RecordAccountPaymentAsync(new AccountPaymentRequest(
            customer.CustomerId,
            invoice.InvoiceId,
            Amount: 400m,
            PaymentMethod.Cash,
            Reference: null,
            harness.CashierId,
            UserRole.Cashier));

        var history = await harness.Service.GetPaymentHistoryAsync(customer.CustomerId);

        Assert.Single(history);
        Assert.Equal("INV-20260401W-0001", history[0].InvoiceNumber);
        Assert.Equal(new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero), history[0].Date);
        Assert.Equal(400m, history[0].Amount);
        Assert.Equal(PaymentStatus.Partial, history[0].Status);
    }

    private static TestHarness CreateHarness()
    {
        return new TestHarness();
    }

    private sealed class TestHarness
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public Guid CashierId { get; } = Guid.NewGuid();
        public InMemoryCustomerAccountDataStore Store { get; } = new();
        public RecordingAuditLogger AuditLogger { get; } = new();
        public CustomerAccountService Service { get; }

        public TestHarness()
        {
            Service = new CustomerAccountService(
                Store,
                new InlineTransactionRunner(),
                AuditLogger,
                new FixedClock(new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero)));
        }
    }

    private sealed class InMemoryCustomerAccountDataStore : ICustomerAccountDataStore
    {
        public Dictionary<Guid, Customer> Customers { get; } = [];
        public Dictionary<Guid, BusinessAccount> BusinessAccounts { get; } = [];
        public List<Invoice> Invoices { get; } = [];
        public List<Payment> Payments { get; } = [];

        public Customer AddCustomer(AccountType accountType)
        {
            var customer = new Customer
            {
                Name = accountType.ToString(),
                Phone = "0110000000",
                WhatsAppNo = "+94770000000",
                CustomerType = CustomerType.Wholesale,
                AccountType = accountType
            };
            Customers[customer.CustomerId] = customer;
            return customer;
        }

        public Invoice AddInvoice(
            Guid customerId,
            string invoiceNumber,
            DateTimeOffset invoiceDate,
            DateTime? dueDate,
            decimal balance)
        {
            var invoice = new Invoice
            {
                CustomerId = customerId,
                CashierId = Guid.NewGuid(),
                InvoiceNumber = invoiceNumber,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                SaleChannel = SaleChannel.Wholesale,
                PaymentMethod = PaymentMethod.Credit,
                GrandTotal = balance,
                BalanceAmount = balance,
                PaymentStatus = balance == 0m ? PaymentStatus.Paid : PaymentStatus.Pending
            };
            Invoices.Add(invoice);
            return invoice;
        }

        public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            Customers[customer.CustomerId] = customer;
            return Task.CompletedTask;
        }

        public Task AddBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default)
        {
            BusinessAccounts[account.CustomerId] = account;
            return Task.CompletedTask;
        }

        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            Customers.TryGetValue(customerId, out var customer);
            return Task.FromResult(customer);
        }

        public Task<BusinessAccount?> GetBusinessAccountAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            BusinessAccounts.TryGetValue(customerId, out var account);
            return Task.FromResult(account);
        }

        public Task UpdateBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default)
        {
            BusinessAccounts[account.CustomerId] = account;
            return Task.CompletedTask;
        }

        public Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Invoices.SingleOrDefault(invoice => invoice.InvoiceId == invoiceId));
        }

        public Task<IReadOnlyList<Invoice>> GetOutstandingInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Invoice> invoices = Invoices
                .Where(invoice => invoice.CustomerId == customerId && invoice.BalanceAmount > 0 && invoice.PaymentStatus != PaymentStatus.Void)
                .ToList();
            return Task.FromResult(invoices);
        }

        public Task<IReadOnlyList<Invoice>> GetAllOutstandingCreditInvoicesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Invoice> invoices = Invoices
                .Where(invoice => invoice.BalanceAmount > 0 && invoice.PaymentStatus != PaymentStatus.Void)
                .ToList();
            return Task.FromResult(invoices);
        }

        public Task UpdateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
        {
            Payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CustomerPaymentRecord>> GetPaymentHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            var records = Payments
                .Where(payment => payment.CustomerId == customerId && payment.PaymentKind == PaymentKind.Payment)
                .Join(
                    Invoices,
                    payment => payment.InvoiceId,
                    invoice => invoice.InvoiceId,
                    (payment, invoice) => new CustomerPaymentRecord(
                        invoice.InvoiceNumber,
                        payment.PaymentDate,
                        payment.Amount,
                        invoice.PaymentStatus))
                .ToList();

            return Task.FromResult<IReadOnlyList<CustomerPaymentRecord>>(records);
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

