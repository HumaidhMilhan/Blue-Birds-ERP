using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Reporting;

public sealed class ReportingService(PoultryProDbContext dbContext) : IReportingService
{
    public async Task<OperationalReportResult> GenerateOperationalReportAsync(
        OperationalReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role != UserRole.Admin)
        {
            throw new InvalidOperationException("Only Admin users can generate operational reports.");
        }

        if (request.FromDate > request.ToDate)
        {
            throw new InvalidOperationException("Report start date cannot be after end date.");
        }

        var from = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var to = request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var invoices = await GetFilteredInvoicesAsync(request, from, to, cancellationToken);
        var invoiceIds = invoices.Select(invoice => invoice.InvoiceId).ToHashSet();
        var batches = await dbContext.Batches.ToDictionaryAsync(batch => batch.BatchId, cancellationToken);
        var products = await dbContext.Products.ToDictionaryAsync(product => product.ProductId, cancellationToken);
        var payments = await dbContext.Payments
            .Where(payment => payment.PaymentDate >= from && payment.PaymentDate < to)
            .Where(payment => !request.PaymentMethod.HasValue || payment.PaymentMethod == request.PaymentMethod)
            .Where(payment => invoiceIds.Contains(payment.InvoiceId))
            .ToListAsync(cancellationToken);
        var wastageRecords = await dbContext.WastageRecords
            .Where(record => record.WastageDate >= from.Date && record.WastageDate < to.Date)
            .Where(record => !request.BatchId.HasValue || record.BatchId == request.BatchId)
            .Where(record => !request.ProductId.HasValue || record.ProductId == request.ProductId)
            .ToListAsync(cancellationToken);
        var salesReturns = await dbContext.SalesReturns
            .Where(salesReturn => salesReturn.ReturnDate >= from && salesReturn.ReturnDate < to)
            .Where(salesReturn => !request.CustomerId.HasValue || salesReturn.CustomerId == request.CustomerId)
            .ToListAsync(cancellationToken);

        return new OperationalReportResult(
            request.FromDate,
            request.ToDate,
            CreateSalesSummary(invoices),
            CreateProfitSummary(invoices, batches),
            CreateWastageSummary(wastageRecords),
            CreateStockOnHand(products.Values, batches.Values, request),
            CreateBatchMovements(products, batches.Values, invoices, wastageRecords, request),
            await CreateDebtorAgingAsync(request.ToDate, cancellationToken),
            CreatePaymentSummary(payments),
            new SalesReturnReportSummary(
                salesReturns.Sum(salesReturn => salesReturn.TotalValue),
                salesReturns.Sum(salesReturn => salesReturn.RefundAmount),
                salesReturns.Count),
            await CreateAuditActivityAsync(request, from, to, cancellationToken));
    }

    private async Task<IReadOnlyList<Invoice>> GetFilteredInvoicesAsync(
        OperationalReportRequest request,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        return await dbContext.Invoices
            .Include(invoice => invoice.Items)
            .Where(invoice => invoice.InvoiceDate >= from && invoice.InvoiceDate < to)
            .Where(invoice => invoice.PaymentStatus != PaymentStatus.Void)
            .Where(invoice => !request.CustomerId.HasValue || invoice.CustomerId == request.CustomerId)
            .Where(invoice => !request.CashierId.HasValue || invoice.CashierId == request.CashierId)
            .Where(invoice => !request.SaleChannel.HasValue || invoice.SaleChannel == request.SaleChannel)
            .Where(invoice => !request.PaymentMethod.HasValue || invoice.PaymentMethod == request.PaymentMethod)
            .Where(invoice => !request.ProductId.HasValue || invoice.Items.Any(item => item.ProductId == request.ProductId))
            .Where(invoice => !request.BatchId.HasValue || invoice.Items.Any(item => item.BatchId == request.BatchId))
            .ToListAsync(cancellationToken);
    }

    private static SalesReportSummary CreateSalesSummary(IReadOnlyList<Invoice> invoices)
    {
        return new SalesReportSummary(
            invoices.Sum(invoice => invoice.GrandTotal),
            invoices.Where(invoice => invoice.SaleChannel == SaleChannel.Retail).Sum(invoice => invoice.GrandTotal),
            invoices.Where(invoice => invoice.SaleChannel == SaleChannel.Wholesale).Sum(invoice => invoice.GrandTotal),
            invoices.Count);
    }

    private static ProfitReportSummary CreateProfitSummary(
        IReadOnlyList<Invoice> invoices,
        IReadOnlyDictionary<Guid, Batch> batches)
    {
        var grossSales = invoices.Sum(invoice => invoice.Items.Sum(item => item.LineTotal));
        var costOfGoods = invoices
            .SelectMany(invoice => invoice.Items)
            .Sum(item => batches.TryGetValue(item.BatchId, out var batch) ? item.Quantity * batch.CostPrice : 0m);

        return new ProfitReportSummary(grossSales, costOfGoods, grossSales - costOfGoods);
    }

    private static WastageReportSummary CreateWastageSummary(IReadOnlyList<WastageRecord> wastageRecords)
    {
        return new WastageReportSummary(
            wastageRecords.Sum(record => record.EstimatedLoss),
            wastageRecords.Where(record => record.WastageType == WastageType.CustomerReturn).Sum(record => record.EstimatedLoss),
            wastageRecords.Count);
    }

    private static IReadOnlyList<StockOnHandReportLine> CreateStockOnHand(
        IEnumerable<Product> products,
        IEnumerable<Batch> batches,
        OperationalReportRequest request)
    {
        return products
            .Where(product => !request.ProductId.HasValue || product.ProductId == request.ProductId)
            .OrderBy(product => product.Name)
            .Select(product => new StockOnHandReportLine(
                product.ProductId,
                product.Name,
                product.UnitOfMeasure,
                batches
                    .Where(batch => batch.ProductId == product.ProductId)
                    .Where(batch => !request.BatchId.HasValue || batch.BatchId == request.BatchId)
                    .Sum(batch => batch.RemainingQuantity),
                product.ReorderLevel))
            .ToList();
    }

    private static IReadOnlyList<BatchMovementReportLine> CreateBatchMovements(
        IReadOnlyDictionary<Guid, Product> products,
        IEnumerable<Batch> batches,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<WastageRecord> wastageRecords,
        OperationalReportRequest request)
    {
        return batches
            .Where(batch => !request.ProductId.HasValue || batch.ProductId == request.ProductId)
            .Where(batch => !request.BatchId.HasValue || batch.BatchId == request.BatchId)
            .OrderBy(batch => products.TryGetValue(batch.ProductId, out var product) ? product.Name : string.Empty)
            .ThenBy(batch => batch.PurchaseDate)
            .Select(batch => new BatchMovementReportLine(
                batch.BatchId,
                batch.ProductId,
                products.TryGetValue(batch.ProductId, out var product) ? product.Name : "Unknown product",
                batch.InitialQuantity,
                invoices.SelectMany(invoice => invoice.Items).Where(item => item.BatchId == batch.BatchId).Sum(item => item.Quantity),
                wastageRecords.Where(record => record.BatchId == batch.BatchId).Sum(record => record.Quantity),
                batch.RemainingQuantity))
            .ToList();
    }

    private async Task<DebtorAgingReport> CreateDebtorAgingAsync(DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var buckets = new Dictionary<string, List<DebtorAgingInvoice>>
        {
            ["Current"] = [],
            ["1-30 days overdue"] = [],
            ["31-60 days overdue"] = [],
            ["61-90 days overdue"] = [],
            ["90+ days overdue"] = []
        };
        var invoices = await dbContext.Invoices
            .Where(invoice => invoice.CustomerId.HasValue && invoice.PaymentStatus != PaymentStatus.Void && invoice.BalanceAmount > 0)
            .OrderBy(invoice => invoice.DueDate)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var customer = await dbContext.Customers.SingleOrDefaultAsync(item => item.CustomerId == invoice.CustomerId, cancellationToken);
            if (customer is null)
            {
                continue;
            }

            buckets[GetAgingBucketName(invoice.DueDate, asOfDate)].Add(new DebtorAgingInvoice(
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
            buckets.Select(bucket => new DebtorAgingBucket(
                bucket.Key,
                bucket.Value.Sum(invoice => invoice.BalanceAmount),
                bucket.Value)).ToList());
    }

    private static PaymentCollectionReportSummary CreatePaymentSummary(IReadOnlyList<Payment> payments)
    {
        return new PaymentCollectionReportSummary(
            payments.Where(payment => payment.PaymentKind == PaymentKind.Payment && payment.PaymentMethod == PaymentMethod.Cash).Sum(payment => payment.Amount),
            payments.Where(payment => payment.PaymentKind == PaymentKind.Payment && payment.PaymentMethod == PaymentMethod.Card).Sum(payment => payment.Amount),
            payments.Where(payment => payment.PaymentKind == PaymentKind.Refund).Sum(payment => payment.Amount),
            payments.Count);
    }

    private async Task<IReadOnlyList<AuditLogEntryResult>> CreateAuditActivityAsync(
        OperationalReportRequest request,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        return await dbContext.AuditLogs
            .Where(log => log.Timestamp >= from && log.Timestamp < to)
            .OrderByDescending(log => log.Timestamp)
            .Select(log => new AuditLogEntryResult(
                log.LogId,
                log.UserId,
                log.Role,
                log.Action,
                log.Module,
                log.TargetEntity,
                log.TargetId,
                log.Timestamp))
            .ToListAsync(cancellationToken);
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
