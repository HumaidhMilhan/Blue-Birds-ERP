using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.BusinessRules;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.POS;

public sealed class POSCheckoutService(
    IPOSDataStore dataStore,
    ITransactionRunner transactionRunner,
    IAuditLogger auditLogger,
    ISystemClock clock) : IPOSCheckoutService
{
    public IReadOnlyList<SaleChannel> GetSaleChannels()
    {
        return PoultryBusinessRules.SaleChannels;
    }

    public IReadOnlyList<PaymentMethod> GetAllowedPaymentMethods(SaleChannel saleChannel)
    {
        return PoultryBusinessRules.GetAllowedPaymentMethods(saleChannel);
    }

    public async Task<IReadOnlyList<BatchPickerOption>> GetBatchPickerOptionsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new POSValidationException("Product is required before selecting a batch.");
        }

        var batches = await dataStore.GetAvailableBatchesAsync(productId, cancellationToken);

        return batches
            .Where(batch => batch.ProductId == productId && batch.Status == BatchStatus.Active && batch.RemainingQuantity > 0)
            .OrderBy(batch => batch.PurchaseDate)
            .ThenBy(batch => batch.ExpiryDate)
            .Select(batch => new BatchPickerOption(
                batch.BatchId,
                batch.ProductId,
                CreateBatchReference(batch.BatchId),
                batch.PurchaseDate,
                batch.ExpiryDate,
                batch.RemainingQuantity))
            .ToList();
    }

    public async Task<CreditSummary?> GetCreditPanelAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new POSValidationException("Customer is required for the credit notification panel.");
        }

        var snapshot = await dataStore.GetCreditPanelSnapshotAsync(customerId, cancellationToken);
        return snapshot is null ? null : CreateCreditSummary(snapshot);
    }

    public async Task<CheckoutResult> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePaymentMethod(request);
        ValidatePaymentAmountsAreNonNegative(request);

        var invoiceDate = clock.Now;
        var prepared = await PrepareCheckoutAsync(request, invoiceDate, cancellationToken);
        ValidatePaymentAmounts(request, prepared.GrandTotal);

        return await transactionRunner.ExecuteAsync(async token =>
        {
            foreach (var deduction in prepared.BatchDeductions)
            {
                deduction.Batch.RemainingQuantity -= deduction.Quantity;
                if (deduction.Batch.RemainingQuantity == 0)
                {
                    deduction.Batch.Status = BatchStatus.Exhausted;
                }

                await dataStore.UpdateBatchAsync(deduction.Batch, token);
            }

            if (prepared.BusinessAccount is not null && prepared.Invoice.BalanceAmount > 0)
            {
                prepared.BusinessAccount.OutstandingBalance += prepared.Invoice.BalanceAmount;
                await dataStore.UpdateBusinessAccountAsync(prepared.BusinessAccount, token);
            }

            await dataStore.AddInvoiceAsync(prepared.Invoice, token);

            foreach (var payment in prepared.Payments)
            {
                await dataStore.AddPaymentAsync(payment, token);
            }

            await auditLogger.WriteAsync(new AuditEntry(
                request.CashierId,
                UserRole.Cashier,
                "INVOICE_CREATE",
                "POS",
                nameof(Invoice),
                prepared.Invoice.InvoiceId,
                null,
                $"{{\"invoiceNumber\":\"{prepared.Invoice.InvoiceNumber}\",\"grandTotal\":{prepared.Invoice.GrandTotal}}}"), token);

            return CreateCheckoutResult(prepared);
        }, cancellationToken);
    }

    public async Task<VoidInvoiceResult> VoidInvoiceAsync(
        VoidInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role != UserRole.Admin)
        {
            throw new POSValidationException("Only Admin users can void invoices.");
        }

        if (request.InvoiceId == Guid.Empty)
        {
            throw new POSValidationException("Invoice is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new POSValidationException("A reason is required to void an invoice.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var invoice = await dataStore.GetInvoiceAsync(request.InvoiceId, token)
                ?? throw new POSValidationException("Invoice was not found.");

            invoice.PaymentStatus = PaymentStatus.Void;
            invoice.VoidReason = request.Reason.Trim();
            invoice.VoidedBy = request.UserId;
            invoice.VoidedAt = clock.Now;

            await dataStore.UpdateInvoiceAsync(invoice, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.UserId,
                request.Role,
                "INVOICE_VOID",
                "POS",
                nameof(Invoice),
                invoice.InvoiceId,
                null,
                $"{{\"invoiceNumber\":\"{invoice.InvoiceNumber}\",\"reason\":\"{invoice.VoidReason}\"}}"), token);

            return new VoidInvoiceResult(
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                invoice.PaymentStatus,
                invoice.VoidReason);
        }, cancellationToken);
    }

    public async Task<SalesReturnResult> ProcessSalesReturnAsync(
        SalesReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role is not (UserRole.Admin or UserRole.Cashier))
        {
            throw new POSValidationException("Only Admin and Cashier users can process sales returns.");
        }

        if (request.InvoiceId == Guid.Empty)
        {
            throw new POSValidationException("Invoice is required.");
        }

        if (request.ProcessedBy == Guid.Empty)
        {
            throw new POSValidationException("The user processing the return is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new POSValidationException("A reason is required to process a sales return.");
        }

        if (request.RefundMethod is not (PaymentMethod.Cash or PaymentMethod.Card))
        {
            throw new POSValidationException("Refund method must be Cash or Card.");
        }

        if (request.Lines.Count == 0)
        {
            throw new POSValidationException("At least one returned item is required.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var invoice = await dataStore.GetInvoiceAsync(request.InvoiceId, token)
                ?? throw new POSValidationException("Invoice was not found.");

            if (invoice.PaymentStatus == PaymentStatus.Void)
            {
                throw new POSValidationException("Invoice is already void.");
            }

            var salesReturn = new SalesReturn
            {
                InvoiceId = invoice.InvoiceId,
                CustomerId = invoice.CustomerId,
                ReturnDate = clock.Now,
                Reason = request.Reason.Trim(),
                ProcessedBy = request.ProcessedBy,
                RefundMethod = request.RefundMethod,
                RefundedAt = clock.Now
            };

            var selectedReturnLines = await SelectReturnLinesAsync(invoice, request.Lines, token);
            var wastageRecords = await CreateReturnWastageRecordsAsync(salesReturn, selectedReturnLines, token);
            var totalReturnValue = selectedReturnLines.Sum(selection => selection.ReturnValue);
            var originalBalanceAmount = invoice.BalanceAmount;
            var balanceReduction = Math.Min(originalBalanceAmount, totalReturnValue);
            var refundablePaidAmount = Math.Max(0m, invoice.PaidAmount - invoice.RefundedAmount);
            var refundAmount = Math.Min(refundablePaidAmount, totalReturnValue - balanceReduction);
            var isFullReturn = await IsFullReturnAfterAsync(invoice, selectedReturnLines, token);

            salesReturn.TotalValue = totalReturnValue;
            salesReturn.RefundAmount = refundAmount;

            for (var index = 0; index < selectedReturnLines.Count; index++)
            {
                var selectedLine = selectedReturnLines[index];
                var wastageRecord = wastageRecords[index];
                salesReturn.Items.Add(new SalesReturnItem
                {
                    ReturnId = salesReturn.ReturnId,
                    InvoiceItemId = selectedLine.InvoiceItem.ItemId,
                    ProductId = selectedLine.InvoiceItem.ProductId,
                    BatchId = selectedLine.InvoiceItem.BatchId,
                    Quantity = selectedLine.Quantity,
                    SoldUnitValue = selectedLine.SoldUnitValue,
                    ReturnValue = selectedLine.ReturnValue,
                    WastageId = wastageRecord.WastageId
                });
            }

            if (isFullReturn)
            {
                invoice.PaymentStatus = PaymentStatus.Void;
                invoice.VoidReason = salesReturn.Reason;
                invoice.VoidedBy = request.ProcessedBy;
                invoice.VoidedAt = clock.Now;
                invoice.BalanceAmount = 0m;
            }
            else
            {
                invoice.GrandTotal = Math.Max(0m, invoice.GrandTotal - totalReturnValue);
                invoice.Subtotal = Math.Max(0m, invoice.Subtotal - totalReturnValue);
                invoice.BalanceAmount = Math.Max(0m, invoice.BalanceAmount - balanceReduction);
                invoice.PaymentStatus = ResolvePaymentStatus(
                    Math.Max(0m, invoice.PaidAmount - invoice.RefundedAmount - refundAmount),
                    invoice.BalanceAmount);
            }

            invoice.RefundedAmount += refundAmount;

            if (invoice.CustomerId.HasValue && invoice.PaymentMethod is PaymentMethod.Credit or PaymentMethod.Mixed)
            {
                var businessAccount = await dataStore.GetBusinessAccountAsync(invoice.CustomerId.Value, token);
                if (businessAccount is not null)
                {
                    businessAccount.OutstandingBalance = Math.Max(0m, businessAccount.OutstandingBalance - balanceReduction);
                    await dataStore.UpdateBusinessAccountAsync(businessAccount, token);
                }
            }

            await dataStore.UpdateInvoiceAsync(invoice, token);
            await dataStore.AddSalesReturnAsync(salesReturn, token);

            foreach (var wastageRecord in wastageRecords)
            {
                await dataStore.AddWastageRecordAsync(wastageRecord, token);
            }

            if (refundAmount > 0)
            {
                await dataStore.AddPaymentAsync(new Payment
                {
                    InvoiceId = invoice.InvoiceId,
                    CustomerId = invoice.CustomerId ?? Guid.Empty,
                    PaymentDate = clock.Now,
                    Amount = refundAmount,
                    PaymentMethod = request.RefundMethod,
                    PaymentKind = PaymentKind.Refund,
                    Reference = $"Refund for {invoice.InvoiceNumber}",
                    RecordedBy = request.ProcessedBy
                }, token);
            }

            await auditLogger.WriteAsync(new AuditEntry(
                request.ProcessedBy,
                request.Role,
                "SALES_RETURN_PROCESS",
                "POS",
                nameof(SalesReturn),
                salesReturn.ReturnId,
                null,
                $"{{\"invoiceNumber\":\"{invoice.InvoiceNumber}\",\"returnValue\":{totalReturnValue},\"refundAmount\":{refundAmount},\"balanceReduction\":{balanceReduction},\"isFullReturn\":{isFullReturn.ToString().ToLowerInvariant()},\"wastageCount\":{wastageRecords.Count}}}"), token);

            if (isFullReturn)
            {
                await auditLogger.WriteAsync(new AuditEntry(
                    request.ProcessedBy,
                    request.Role,
                    "INVOICE_VOID",
                    "POS",
                    nameof(Invoice),
                    invoice.InvoiceId,
                    null,
                    $"{{\"invoiceNumber\":\"{invoice.InvoiceNumber}\",\"reason\":\"{invoice.VoidReason}\"}}"), token);
            }

            return new SalesReturnResult(
                salesReturn.ReturnId,
                invoice.InvoiceId,
                invoice.InvoiceNumber,
                invoice.PaymentStatus,
                isFullReturn,
                totalReturnValue,
                refundAmount,
                balanceReduction,
                invoice.BalanceAmount,
                wastageRecords.Select(record => record.WastageId).ToList());
        }, cancellationToken);
    }

    private async Task<PreparedCheckout> PrepareCheckoutAsync(
        CheckoutRequest request,
        DateTimeOffset invoiceDate,
        CancellationToken cancellationToken)
    {
        if (request.CashierId == Guid.Empty)
        {
            throw new POSValidationException("Cashier is required.");
        }

        if (request.Lines.Count == 0)
        {
            throw new POSValidationException("At least one invoice line is required.");
        }

        Customer? customer = null;
        BusinessAccount? businessAccount = null;
        CreditSummary? creditPanel = null;

        if (request.CustomerId.HasValue)
        {
            customer = await dataStore.GetCustomerAsync(request.CustomerId.Value, cancellationToken)
                ?? throw new POSValidationException("Customer was not found.");

            businessAccount = await dataStore.GetBusinessAccountAsync(customer.CustomerId, cancellationToken);
            creditPanel = await GetCreditPanelAsync(customer.CustomerId, cancellationToken);
        }

        var requiresCreditDueDate = PoultryBusinessRules.RequiresCreditDueDate(request.PaymentMethod);
        if (requiresCreditDueDate && customer is null)
        {
            throw new POSValidationException("Credit invoices require a customer.");
        }

        var dueDate = ResolveDueDate(request, invoiceDate, customer, businessAccount);
        var preparedLines = await PrepareLinesAsync(request.Lines, cancellationToken);
        var grandTotal = preparedLines.Sum(line => line.InvoiceItem.LineTotal);
        var discountTotal = preparedLines.Sum(line => line.InvoiceItem.DiscountAmount);
        var paidAmount = request.CashAmount + request.CardAmount;
        var balanceAmount = request.CreditAmount;
        var invoiceStatus = ResolvePaymentStatus(paidAmount, balanceAmount);
        var invoiceDay = DateOnly.FromDateTime(invoiceDate.Date);
        var sequence = await dataStore.GetNextInvoiceSequenceAsync(invoiceDay, request.SaleChannel, cancellationToken);
        var invoiceId = Guid.NewGuid();
        var invoiceNumber = PoultryBusinessRules.CreateInvoiceNumber(invoiceDay, request.SaleChannel, sequence);

        foreach (var line in preparedLines)
        {
            line.InvoiceItem.InvoiceId = invoiceId;
        }

        var invoice = new Invoice
        {
            InvoiceId = invoiceId,
            InvoiceNumber = invoiceNumber,
            CustomerId = customer?.CustomerId,
            CashierId = request.CashierId,
            SaleChannel = request.SaleChannel,
            PaymentMethod = request.PaymentMethod,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Subtotal = grandTotal + discountTotal,
            DiscountTotal = discountTotal,
            GrandTotal = grandTotal,
            PaidAmount = paidAmount,
            BalanceAmount = balanceAmount,
            PaymentStatus = invoiceStatus,
            Notes = request.Notes,
            Items = preparedLines.Select(line => line.InvoiceItem).ToList()
        };

        var payments = CreatePayments(request, invoice, customer).ToList();
        var batchDeductions = preparedLines
            .GroupBy(line => line.Batch.BatchId)
            .Select(group => new BatchDeduction(group.First().Batch, group.Sum(line => line.InvoiceItem.Quantity)))
            .ToList();

        foreach (var deduction in batchDeductions)
        {
            PoultryBusinessRules.EnsureCanDeductStock(deduction.Batch, deduction.Quantity);
        }

        var receipt = CreateReceipt(invoice, customer);
        var updatedCreditPanel = UpdateCreditPanelForCheckout(creditPanel, businessAccount, balanceAmount);

        return new PreparedCheckout(
            invoice,
            payments,
            batchDeductions,
            businessAccount,
            receipt,
            updatedCreditPanel,
            grandTotal);
    }

    private async Task<IReadOnlyList<PreparedLine>> PrepareLinesAsync(
        IReadOnlyCollection<CheckoutLineItem> lines,
        CancellationToken cancellationToken)
    {
        var preparedLines = new List<PreparedLine>();

        foreach (var line in lines)
        {
            if (line.ProductId == Guid.Empty)
            {
                throw new POSValidationException("Product is required for each invoice line.");
            }

            if (line.BatchId == Guid.Empty)
            {
                throw new POSValidationException("A cashier-selected batch is required for each invoice line.");
            }

            if (line.Quantity <= 0)
            {
                throw new POSValidationException("Line quantity must be greater than zero.");
            }

            if (line.DiscountAmount < 0)
            {
                throw new POSValidationException("Line discount cannot be negative.");
            }

            var product = await dataStore.GetProductAsync(line.ProductId, cancellationToken)
                ?? throw new POSValidationException("Product was not found.");
            var batch = await dataStore.GetBatchAsync(line.BatchId, cancellationToken)
                ?? throw new POSValidationException("Batch was not found.");

            if (!product.IsActive)
            {
                throw new POSValidationException("Inactive products cannot be sold.");
            }

            if (batch.ProductId != product.ProductId)
            {
                throw new POSValidationException("Selected batch does not belong to the selected product.");
            }

            if (batch.Status != BatchStatus.Active)
            {
                throw new POSValidationException("Selected batch is not active.");
            }

            var grossLineTotal = line.Quantity * product.SellingPrice;
            if (line.DiscountAmount > grossLineTotal)
            {
                throw new POSValidationException("Line discount cannot exceed the line total.");
            }

            preparedLines.Add(new PreparedLine(
                batch,
                new InvoiceItem
                {
                    ProductId = product.ProductId,
                    BatchId = batch.BatchId,
                    ProductName = product.Name,
                    BatchReference = CreateBatchReference(batch.BatchId),
                    UnitOfMeasure = product.UnitOfMeasure,
                    Quantity = line.Quantity,
                    UnitPrice = product.SellingPrice,
                    DiscountAmount = line.DiscountAmount,
                    LineTotal = grossLineTotal - line.DiscountAmount
                }));
        }

        return preparedLines;
    }

    private static void ValidatePaymentMethod(CheckoutRequest request)
    {
        if (!PoultryBusinessRules.SaleChannels.Contains(request.SaleChannel))
        {
            throw new POSValidationException("A valid sale channel must be selected before checkout.");
        }

        if (!PoultryBusinessRules.IsPaymentMethodAllowed(request.SaleChannel, request.PaymentMethod))
        {
            throw new POSValidationException($"{request.PaymentMethod} is not allowed for {request.SaleChannel} sales.");
        }
    }

    private static void ValidatePaymentAmountsAreNonNegative(CheckoutRequest request)
    {
        if (request.CashAmount < 0 || request.CardAmount < 0 || request.CreditAmount < 0)
        {
            throw new POSValidationException("Payment amounts cannot be negative.");
        }
    }

    private static void ValidatePaymentAmounts(CheckoutRequest request, decimal grandTotal)
    {
        var paidAmount = request.CashAmount + request.CardAmount;
        var tenderedTotal = paidAmount + request.CreditAmount;

        if (tenderedTotal != grandTotal)
        {
            throw new POSValidationException("Cash, card, and credit amounts must equal the invoice grand total.");
        }

        switch (request.PaymentMethod)
        {
            case PaymentMethod.Cash when request.CashAmount != grandTotal || request.CardAmount != 0 || request.CreditAmount != 0:
                throw new POSValidationException("Cash sales must be fully paid by cash.");
            case PaymentMethod.Card when request.CardAmount != grandTotal || request.CashAmount != 0 || request.CreditAmount != 0:
                throw new POSValidationException("Card sales must be fully paid by card.");
            case PaymentMethod.Credit when request.CreditAmount != grandTotal || paidAmount != 0:
                throw new POSValidationException("Credit sales must place the full invoice value on credit.");
            case PaymentMethod.Mixed when paidAmount <= 0 || request.CreditAmount <= 0:
                throw new POSValidationException("Mixed wholesale sales require a cash/card portion and a credit portion.");
        }
    }

    private static DateTime? ResolveDueDate(
        CheckoutRequest request,
        DateTimeOffset invoiceDate,
        Customer? customer,
        BusinessAccount? businessAccount)
    {
        if (!PoultryBusinessRules.RequiresCreditDueDate(request.PaymentMethod))
        {
            return null;
        }

        if (businessAccount is not null)
        {
            return invoiceDate.Date.AddDays(businessAccount.CreditPeriodDays);
        }

        if (customer?.AccountType == AccountType.OneTimeCreditor)
        {
            if (!request.ManualDueDate.HasValue)
            {
                throw new POSValidationException("One-time creditor credit invoices require a manually entered due date.");
            }

            if (request.ManualDueDate.Value.Date < invoiceDate.Date)
            {
                throw new POSValidationException("Manual due date cannot be earlier than the invoice date.");
            }

            return request.ManualDueDate.Value.Date;
        }

        throw new POSValidationException("Credit invoices require either a Business Account or One-Time Creditor customer.");
    }

    private static PaymentStatus ResolvePaymentStatus(decimal paidAmount, decimal balanceAmount)
    {
        if (balanceAmount == 0)
        {
            return PaymentStatus.Paid;
        }

        return paidAmount > 0 ? PaymentStatus.Partial : PaymentStatus.Pending;
    }

    private static IEnumerable<Payment> CreatePayments(CheckoutRequest request, Invoice invoice, Customer? customer)
    {
        if (customer is null && (request.CashAmount > 0 || request.CardAmount > 0))
        {
            customer = new Customer { CustomerId = Guid.Empty };
        }

        if (request.CashAmount > 0)
        {
            yield return new Payment
            {
                InvoiceId = invoice.InvoiceId,
                CustomerId = customer?.CustomerId ?? Guid.Empty,
                PaymentDate = invoice.InvoiceDate,
                Amount = request.CashAmount,
                PaymentMethod = PaymentMethod.Cash,
                PaymentKind = PaymentKind.Payment,
                RecordedBy = invoice.CashierId
            };
        }

        if (request.CardAmount > 0)
        {
            yield return new Payment
            {
                InvoiceId = invoice.InvoiceId,
                CustomerId = customer?.CustomerId ?? Guid.Empty,
                PaymentDate = invoice.InvoiceDate,
                Amount = request.CardAmount,
                PaymentMethod = PaymentMethod.Card,
                PaymentKind = PaymentKind.Payment,
                RecordedBy = invoice.CashierId
            };
        }
    }

    private async Task<IReadOnlyList<SelectedReturnLine>> SelectReturnLinesAsync(
        Invoice invoice,
        IReadOnlyCollection<SalesReturnLine> returnLines,
        CancellationToken cancellationToken)
    {
        var selections = new List<SelectedReturnLine>();

        foreach (var returnLineGroup in returnLines.GroupBy(line => line.InvoiceItemId))
        {
            if (returnLineGroup.Key == Guid.Empty)
            {
                throw new POSValidationException("Returned invoice item is required.");
            }

            var returnedQuantity = returnLineGroup.Sum(line => line.Quantity);
            if (returnedQuantity <= 0)
            {
                throw new POSValidationException("Returned quantity must be greater than zero.");
            }

            var invoiceItem = invoice.Items.SingleOrDefault(item => item.ItemId == returnLineGroup.Key)
                ?? throw new POSValidationException("Returned item does not belong to the invoice.");
            var previouslyReturnedQuantity = await dataStore.GetPreviouslyReturnedQuantityAsync(invoiceItem.ItemId, cancellationToken);

            if (previouslyReturnedQuantity + returnedQuantity > invoiceItem.Quantity)
            {
                throw new POSValidationException("Returned quantity cannot exceed the invoiced quantity.");
            }

            var netUnitValue = invoiceItem.LineTotal / invoiceItem.Quantity;
            selections.Add(new SelectedReturnLine(
                invoiceItem,
                returnedQuantity,
                netUnitValue,
                returnedQuantity * netUnitValue));
        }

        return selections;
    }

    private async Task<bool> IsFullReturnAfterAsync(
        Invoice invoice,
        IReadOnlyCollection<SelectedReturnLine> selectedReturnLines,
        CancellationToken cancellationToken)
    {
        foreach (var invoiceItem in invoice.Items)
        {
            var previouslyReturnedQuantity = await dataStore.GetPreviouslyReturnedQuantityAsync(invoiceItem.ItemId, cancellationToken);
            var currentReturnedQuantity = selectedReturnLines
                .Where(line => line.InvoiceItem.ItemId == invoiceItem.ItemId)
                .Sum(line => line.Quantity);

            if (previouslyReturnedQuantity + currentReturnedQuantity < invoiceItem.Quantity)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyList<WastageRecord>> CreateReturnWastageRecordsAsync(
        SalesReturn salesReturn,
        IReadOnlyCollection<SelectedReturnLine> selectedReturnLines,
        CancellationToken cancellationToken)
    {
        var records = new List<WastageRecord>();

        foreach (var selectedLine in selectedReturnLines)
        {
            var batch = await dataStore.GetBatchAsync(selectedLine.InvoiceItem.BatchId, cancellationToken)
                ?? throw new POSValidationException("Returned item batch was not found.");

            records.Add(PoultryBusinessRules.CreateCustomerReturnWastage(
                salesReturn,
                selectedLine.InvoiceItem,
                selectedLine.Quantity,
                batch.CostPrice));
        }

        return records;
    }

    private static CreditSummary CreateCreditSummary(CreditPanelSnapshot snapshot)
    {
        var availableCredit = snapshot.CreditLimit.HasValue
            ? snapshot.CreditLimit.Value - snapshot.OutstandingBalance
            : (decimal?)null;
        var isLimitActive = snapshot.CreditLimit.HasValue &&
            snapshot.OutstandingBalance >= snapshot.CreditLimit.Value;

        return new CreditSummary(
            snapshot.CustomerId,
            snapshot.OutstandingBalance,
            snapshot.CreditLimit,
            availableCredit,
            snapshot.OverdueInvoiceCount,
            snapshot.LastPaymentDate,
            isLimitActive,
            IsLimitBlocking: false);
    }

    private static CreditSummary? UpdateCreditPanelForCheckout(
        CreditSummary? currentPanel,
        BusinessAccount? businessAccount,
        decimal newBalance)
    {
        if (currentPanel is null)
        {
            return null;
        }

        var outstanding = currentPanel.OutstandingBalance + newBalance;
        var availableCredit = currentPanel.CreditLimit.HasValue
            ? currentPanel.CreditLimit.Value - outstanding
            : (decimal?)null;
        var isLimitActive = currentPanel.CreditLimit.HasValue &&
            outstanding >= currentPanel.CreditLimit.Value;

        if (businessAccount is not null)
        {
            availableCredit = businessAccount.CreditLimit - (businessAccount.OutstandingBalance + newBalance);
            isLimitActive = businessAccount.CreditLimit > 0 &&
                businessAccount.OutstandingBalance + newBalance >= businessAccount.CreditLimit;
        }

        return currentPanel with
        {
            OutstandingBalance = outstanding,
            AvailableCredit = availableCredit,
            IsLimitAlertActive = isLimitActive,
            IsLimitBlocking = false
        };
    }

    private static InvoiceReceipt CreateReceipt(Invoice invoice, Customer? customer)
    {
        return new InvoiceReceipt(
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.SaleChannel,
            customer is null
                ? null
                : new ReceiptCustomer(
                    customer.CustomerId,
                    customer.Name,
                    customer.Phone,
                    customer.WhatsAppNo,
                    customer.Address),
            invoice.Items
                .Select(item => new InvoiceReceiptLine(
                    item.ProductName,
                    item.BatchReference,
                    item.Quantity,
                    item.UnitOfMeasure,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.LineTotal))
                .ToList(),
            invoice.Subtotal,
            invoice.DiscountTotal,
            invoice.GrandTotal,
            invoice.PaymentMethod,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            invoice.DueDate);
    }

    private static CheckoutResult CreateCheckoutResult(PreparedCheckout prepared)
    {
        return new CheckoutResult(
            prepared.Invoice.InvoiceId,
            prepared.Invoice.InvoiceNumber,
            prepared.Invoice.GrandTotal,
            prepared.Invoice.PaidAmount,
            prepared.Invoice.BalanceAmount,
            prepared.Invoice.PaymentStatus,
            prepared.Invoice.DueDate,
            prepared.Receipt,
            prepared.CreditPanel);
    }

    private static string CreateBatchReference(Guid batchId)
    {
        return batchId.ToString("N")[..8].ToUpperInvariant();
    }

    private sealed record PreparedLine(Batch Batch, InvoiceItem InvoiceItem);

    private sealed record BatchDeduction(Batch Batch, decimal Quantity);

    private sealed record SelectedReturnLine(
        InvoiceItem InvoiceItem,
        decimal Quantity,
        decimal SoldUnitValue,
        decimal ReturnValue);

    private sealed record PreparedCheckout(
        Invoice Invoice,
        IReadOnlyCollection<Payment> Payments,
        IReadOnlyCollection<BatchDeduction> BatchDeductions,
        BusinessAccount? BusinessAccount,
        InvoiceReceipt Receipt,
        CreditSummary? CreditPanel,
        decimal GrandTotal);
}
