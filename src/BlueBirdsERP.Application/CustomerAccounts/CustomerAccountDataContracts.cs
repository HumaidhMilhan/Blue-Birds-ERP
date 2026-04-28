using BlueBirdsERP.Domain.Entities;

namespace BlueBirdsERP.Application.CustomerAccounts;

public interface ICustomerAccountDataStore
{
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task AddBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<BusinessAccount?> GetBusinessAccountAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task UpdateBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default);
    Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetOutstandingInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetAllOutstandingCreditInvoicesAsync(CancellationToken cancellationToken = default);
    Task UpdateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerPaymentRecord>> GetPaymentHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public sealed record CustomerPaymentRecord(
    string InvoiceNumber,
    DateTimeOffset Date,
    decimal Amount,
    BlueBirdsERP.Domain.Enums.PaymentStatus Status);

