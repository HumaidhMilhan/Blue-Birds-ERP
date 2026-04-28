using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.POS;

public interface IPOSDataStore
{
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> GetAvailableBatchesAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<BusinessAccount?> GetBusinessAccountAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CreditPanelSnapshot?> GetCreditPanelSnapshotAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<int> GetNextInvoiceSequenceAsync(DateOnly invoiceDate, SaleChannel saleChannel, CancellationToken cancellationToken = default);
    Task AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
    Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default);
    Task AddWastageRecordAsync(WastageRecord wastageRecord, CancellationToken cancellationToken = default);
    Task<decimal> GetPreviouslyReturnedQuantityAsync(Guid invoiceItemId, CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(Batch batch, CancellationToken cancellationToken = default);
    Task UpdateBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default);
    Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task UpdateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
}

public interface ITransactionRunner
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}

public interface ISystemClock
{
    DateTimeOffset Now { get; }
}

public sealed record CreditPanelSnapshot(
    Guid CustomerId,
    decimal OutstandingBalance,
    decimal? CreditLimit,
    int OverdueInvoiceCount,
    DateTimeOffset? LastPaymentDate);
