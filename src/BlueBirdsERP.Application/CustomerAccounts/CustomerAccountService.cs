using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.CustomerAccounts;

public sealed class CustomerAccountService(
    ICustomerAccountDataStore dataStore,
    ITransactionRunner transactionRunner,
    IAuditLogger auditLogger,
    ISystemClock clock) : ICustomerAccountService
{
    private static readonly string[] AgingBucketNames =
    [
        "Current",
        "1-30 days overdue",
        "31-60 days overdue",
        "61-90 days overdue",
        "90+ days overdue"
    ];

    public async Task<CustomerAccountResult> CreateBusinessAccountAsync(
        CreateBusinessAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateRequired(request.Name, "Business account name is required.");
        ValidateRequired(request.Phone, "Phone number is required.");
        ValidateRequired(request.WhatsAppNo, "WhatsApp number is required.");
        ValidateBusinessTerms(request.CreditLimit, request.CreditPeriodDays, request.NotificationLeadDays);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var customer = new Customer
            {
                Name = request.Name.Trim(),
                Phone = request.Phone.Trim(),
                WhatsAppNo = request.WhatsAppNo.Trim(),
                Email = NormalizeOptional(request.Email),
                Address = NormalizeOptional(request.Address),
                CustomerType = CustomerType.Wholesale,
                AccountType = AccountType.BusinessAccount,
                CreatedAt = clock.Now
            };

            var account = new BusinessAccount
            {
                CustomerId = customer.CustomerId,
                CreditLimit = request.CreditLimit,
                CreditPeriodDays = request.CreditPeriodDays,
                NotificationLeadDays = request.NotificationLeadDays,
                OutstandingBalance = 0m,
                Status = BusinessAccountStatus.Active,
                IsActive = true
            };

            await dataStore.AddCustomerAsync(customer, token);
            await dataStore.AddBusinessAccountAsync(account, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.CreatedBy,
                request.Role,
                "BUSINESS_ACCOUNT_CREATE",
                "ACCOUNTS",
                nameof(BusinessAccount),
                account.AccountId,
                null,
                $"{{\"customerId\":\"{customer.CustomerId}\",\"creditLimit\":{account.CreditLimit}}}"), token);

            return CreateResult(customer, account);
        }, cancellationToken);
    }

    public async Task<CustomerAccountResult> CreateOneTimeCreditorAsync(
        CreateOneTimeCreditorRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateRequired(request.FullName, "Full name is required.");
        ValidateRequired(request.Phone, "Phone number is required.");
        ValidateRequired(request.WhatsAppNo, "WhatsApp number is required.");
        ValidateRequired(request.Address, "Address is required.");

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var customer = new Customer
            {
                Name = request.FullName.Trim(),
                Phone = request.Phone.Trim(),
                WhatsAppNo = request.WhatsAppNo.Trim(),
                Address = NormalizeOptional(request.Address),
                NicOrBusinessRegistrationNumber = NormalizeOptional(request.NicOrBusinessRegistrationNumber),
                CustomerType = CustomerType.Wholesale,
                AccountType = AccountType.OneTimeCreditor,
                CreatedAt = clock.Now
            };

            await dataStore.AddCustomerAsync(customer, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.CreatedBy,
                request.Role,
                "ONE_TIME_CREDITOR_CREATE",
                "ACCOUNTS",
                nameof(Customer),
                customer.CustomerId,
                null,
                $"{{\"customerId\":\"{customer.CustomerId}\"}}"), token);

            return CreateResult(customer, account: null);
        }, cancellationToken);
    }

    public async Task<CreditSummary> GetCreditSummaryAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetRequiredCustomerAsync(customerId, cancellationToken);
        var businessAccount = await dataStore.GetBusinessAccountAsync(customerId, cancellationToken);
        var invoices = await dataStore.GetOutstandingInvoicesAsync(customerId, cancellationToken);
        var outstandingFromInvoices = invoices.Sum(invoice => invoice.BalanceAmount);

        if (businessAccount is not null)
        {
            var availableCredit = businessAccount.CreditLimit - businessAccount.OutstandingBalance;
            return new CreditSummary(
                customer.CustomerId,
                businessAccount.OutstandingBalance,
                businessAccount.CreditLimit,
                availableCredit,
                invoices.Count(IsOverdueAsOfToday),
                null,
                businessAccount.CreditLimit > 0 && businessAccount.OutstandingBalance >= businessAccount.CreditLimit,
                IsLimitBlocking: false);
        }

        return new CreditSummary(
            customer.CustomerId,
            outstandingFromInvoices,
            CreditLimit: null,
            AvailableCredit: null,
            invoices.Count(IsOverdueAsOfToday),
            null,
            IsLimitAlertActive: false,
            IsLimitBlocking: false);
    }

    public async Task<AccountPaymentResult> RecordAccountPaymentAsync(
        AccountPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsurePaymentRole(request.Role);
        if (request.Amount <= 0)
        {
            throw new CustomerAccountValidationException("Payment amount must be greater than zero.");
        }

        if (request.PaymentMethod is not (PaymentMethod.Cash or PaymentMethod.Card))
        {
            throw new CustomerAccountValidationException("Account payments must be recorded as Cash or Card.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var customer = await GetRequiredCustomerAsync(request.CustomerId, token);
            if (customer.AccountType is not (AccountType.BusinessAccount or AccountType.OneTimeCreditor))
            {
                throw new CustomerAccountValidationException("Payments can only be recorded for credit customers.");
            }

            var invoices = await SelectInvoicesForPaymentAsync(request, token);
            var totalOutstanding = invoices.Sum(invoice => invoice.BalanceAmount);
            if (request.Amount > totalOutstanding)
            {
                throw new CustomerAccountValidationException("Overpayments are not allowed.");
            }

            var remainingPayment = request.Amount;
            var allocations = new List<InvoicePaymentAllocation>();

            foreach (var invoice in invoices)
            {
                if (remainingPayment <= 0)
                {
                    break;
                }

                var applied = Math.Min(invoice.BalanceAmount, remainingPayment);
                invoice.PaidAmount += applied;
                invoice.BalanceAmount -= applied;
                invoice.PaymentStatus = invoice.BalanceAmount == 0m ? PaymentStatus.Paid : PaymentStatus.Partial;
                remainingPayment -= applied;

                await dataStore.UpdateInvoiceAsync(invoice, token);
                await dataStore.AddPaymentAsync(new Payment
                {
                    InvoiceId = invoice.InvoiceId,
                    CustomerId = customer.CustomerId,
                    PaymentDate = clock.Now,
                    Amount = applied,
                    PaymentMethod = request.PaymentMethod,
                    PaymentKind = PaymentKind.Payment,
                    Reference = request.Reference,
                    RecordedBy = request.RecordedBy
                }, token);

                allocations.Add(new InvoicePaymentAllocation(
                    invoice.InvoiceId,
                    invoice.InvoiceNumber,
                    applied,
                    invoice.BalanceAmount,
                    invoice.PaymentStatus));
            }

            var businessAccount = await dataStore.GetBusinessAccountAsync(customer.CustomerId, token);
            if (businessAccount is not null)
            {
                businessAccount.OutstandingBalance = Math.Max(0m, businessAccount.OutstandingBalance - request.Amount);
                await dataStore.UpdateBusinessAccountAsync(businessAccount, token);
            }

            await auditLogger.WriteAsync(new AuditEntry(
                request.RecordedBy,
                request.Role,
                "ACCOUNT_PAYMENT_RECORD",
                "ACCOUNTS",
                nameof(Payment),
                allocations.First().InvoiceId,
                null,
                $"{{\"customerId\":\"{customer.CustomerId}\",\"amount\":{request.Amount}}}"), token);

            var remainingOutstanding = businessAccount?.OutstandingBalance ??
                (await dataStore.GetOutstandingInvoicesAsync(customer.CustomerId, token)).Sum(invoice => invoice.BalanceAmount);

            return new AccountPaymentResult(
                customer.CustomerId,
                request.Amount,
                remainingOutstanding,
                allocations);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerPaymentHistoryEntry>> GetPaymentHistoryAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        await GetRequiredCustomerAsync(customerId, cancellationToken);
        var paymentRecords = await dataStore.GetPaymentHistoryAsync(customerId, cancellationToken);

        return paymentRecords
            .OrderByDescending(record => record.Date)
            .Select(record => new CustomerPaymentHistoryEntry(
                record.InvoiceNumber,
                record.Date,
                record.Amount,
                record.Status))
            .ToList();
    }

    public async Task<CustomerAccountResult> UpdateBusinessAccountTermsAsync(
        UpdateBusinessAccountTermsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateBusinessTerms(request.CreditLimit, request.CreditPeriodDays, request.NotificationLeadDays);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var customer = await GetRequiredCustomerAsync(request.CustomerId, token);
            var account = await dataStore.GetBusinessAccountAsync(request.CustomerId, token)
                ?? throw new CustomerAccountValidationException("Business Account was not found.");

            var before = $"{{\"creditLimit\":{account.CreditLimit},\"creditPeriodDays\":{account.CreditPeriodDays},\"notificationLeadDays\":{account.NotificationLeadDays}}}";

            account.CreditLimit = request.CreditLimit;
            account.CreditPeriodDays = request.CreditPeriodDays;
            account.NotificationLeadDays = request.NotificationLeadDays;
            await dataStore.UpdateBusinessAccountAsync(account, token);

            var outstandingInvoices = await dataStore.GetOutstandingInvoicesAsync(customer.CustomerId, token);
            foreach (var invoice in outstandingInvoices.Where(invoice => invoice.PaymentStatus != PaymentStatus.Void))
            {
                invoice.DueDate = invoice.InvoiceDate.Date.AddDays(request.CreditPeriodDays);
                await dataStore.UpdateInvoiceAsync(invoice, token);
            }

            var after = $"{{\"creditLimit\":{account.CreditLimit},\"creditPeriodDays\":{account.CreditPeriodDays},\"notificationLeadDays\":{account.NotificationLeadDays},\"recalculatedInvoices\":{outstandingInvoices.Count}}}";
            await auditLogger.WriteAsync(new AuditEntry(
                request.UpdatedBy,
                request.Role,
                "BUSINESS_ACCOUNT_TERMS_EDIT",
                "ACCOUNTS",
                nameof(BusinessAccount),
                account.AccountId,
                before,
                after), token);

            return CreateResult(customer, account);
        }, cancellationToken);
    }

    public async Task<DebtorAgingReport> GenerateDebtorAgingReportAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var invoices = await dataStore.GetAllOutstandingCreditInvoicesAsync(cancellationToken);
        var buckets = AgingBucketNames.ToDictionary(
            name => name,
            name => new List<DebtorAgingInvoice>());

        foreach (var invoice in invoices.Where(invoice => invoice.BalanceAmount > 0 && invoice.PaymentStatus != PaymentStatus.Void))
        {
            if (!invoice.CustomerId.HasValue)
            {
                continue;
            }

            var customer = await dataStore.GetCustomerAsync(invoice.CustomerId.Value, cancellationToken);
            if (customer is null)
            {
                continue;
            }

            var bucketName = GetAgingBucketName(invoice.DueDate, asOfDate);
            buckets[bucketName].Add(new DebtorAgingInvoice(
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                customer.CustomerId,
                customer.Name,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.BalanceAmount));
        }

        return new DebtorAgingReport(
            asOfDate,
            AgingBucketNames
                .Select(name => new DebtorAgingBucket(
                    name,
                    buckets[name].Sum(invoice => invoice.BalanceAmount),
                    buckets[name]
                        .OrderBy(invoice => invoice.DueDate)
                        .ThenBy(invoice => invoice.InvoiceDate)
                        .ToList()))
                .ToList());
    }

    private async Task<IReadOnlyList<Invoice>> SelectInvoicesForPaymentAsync(
        AccountPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.InvoiceId.HasValue)
        {
            var invoice = await dataStore.GetInvoiceAsync(request.InvoiceId.Value, cancellationToken)
                ?? throw new CustomerAccountValidationException("Invoice was not found.");

            if (invoice.CustomerId != request.CustomerId)
            {
                throw new CustomerAccountValidationException("Invoice does not belong to the selected customer.");
            }

            if (invoice.PaymentStatus == PaymentStatus.Void || invoice.BalanceAmount <= 0)
            {
                throw new CustomerAccountValidationException("Invoice has no outstanding balance.");
            }

            return [invoice];
        }

        return (await dataStore.GetOutstandingInvoicesAsync(request.CustomerId, cancellationToken))
            .Where(invoice => invoice.PaymentStatus != PaymentStatus.Void && invoice.BalanceAmount > 0)
            .OrderBy(invoice => invoice.InvoiceDate)
            .ThenBy(invoice => invoice.DueDate)
            .ThenBy(invoice => invoice.InvoiceNumber)
            .ToList();
    }

    private async Task<Customer> GetRequiredCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            throw new CustomerAccountValidationException("Customer is required.");
        }

        return await dataStore.GetCustomerAsync(customerId, cancellationToken)
            ?? throw new CustomerAccountValidationException("Customer was not found.");
    }

    private static CustomerAccountResult CreateResult(Customer customer, BusinessAccount? account)
    {
        return new CustomerAccountResult(
            customer.CustomerId,
            account?.AccountId,
            customer.Name,
            customer.AccountType,
            account?.OutstandingBalance ?? 0m,
            account?.CreditLimit,
            account is null ? null : account.CreditLimit - account.OutstandingBalance);
    }

    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new CustomerAccountValidationException("Only Admin users can manage customer accounts.");
        }
    }

    private static void EnsurePaymentRole(UserRole role)
    {
        if (role is not (UserRole.Admin or UserRole.Cashier))
        {
            throw new CustomerAccountValidationException("Only Admin and Cashier users can record account payments.");
        }
    }

    private static void ValidateBusinessTerms(decimal creditLimit, int creditPeriodDays, int notificationLeadDays)
    {
        if (creditLimit < 0)
        {
            throw new CustomerAccountValidationException("Credit limit cannot be negative.");
        }

        if (creditPeriodDays <= 0)
        {
            throw new CustomerAccountValidationException("Credit period must be greater than zero days.");
        }

        if (notificationLeadDays < 0)
        {
            throw new CustomerAccountValidationException("Notification lead period cannot be negative.");
        }
    }

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CustomerAccountValidationException(message);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool IsOverdueAsOfToday(Invoice invoice)
    {
        return invoice.DueDate.HasValue &&
            invoice.DueDate.Value.Date < clock.Now.Date &&
            invoice.BalanceAmount > 0 &&
            invoice.PaymentStatus != PaymentStatus.Void;
    }

    private static string GetAgingBucketName(DateTime? dueDate, DateOnly asOfDate)
    {
        if (!dueDate.HasValue)
        {
            return "Current";
        }

        var daysOverdue = asOfDate.DayNumber - DateOnly.FromDateTime(dueDate.Value.Date).DayNumber;
        return daysOverdue switch
        {
            <= 0 => "Current",
            <= 30 => "1-30 days overdue",
            <= 60 => "31-60 days overdue",
            <= 90 => "61-90 days overdue",
            _ => "90+ days overdue"
        };
    }
}
