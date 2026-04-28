using BlueBirdsERP.Application.CustomerAccounts;
using BlueBirdsERP.Application.Inventory;
using BlueBirdsERP.Application.Notifications;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Application.Security;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class EfCoreDataStore(PoultryProDbContext dbContext) :
    IPOSDataStore,
    IInventoryDataStore,
    ICustomerAccountDataStore,
    INotificationDataStore,
    ISecurityDataStore
{
    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return dbContext.Products.SingleOrDefaultAsync(product => product.ProductId == productId, cancellationToken);
    }

    public Task<Product?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return dbContext.Products.SingleOrDefaultAsync(product => product.Name == name, cancellationToken);
    }

    public Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return dbContext.Batches.SingleOrDefaultAsync(batch => batch.BatchId == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> GetAvailableBatchesAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Batches
            .Where(batch => batch.ProductId == productId && batch.Status == BatchStatus.Active && batch.RemainingQuantity > 0)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.PurchaseDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return dbContext.Customers.SingleOrDefaultAsync(customer => customer.CustomerId == customerId, cancellationToken);
    }

    public Task<BusinessAccount?> GetBusinessAccountAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return dbContext.BusinessAccounts.SingleOrDefaultAsync(account => account.CustomerId == customerId, cancellationToken);
    }

    public async Task<CreditPanelSnapshot?> GetCreditPanelSnapshotAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var account = await GetBusinessAccountAsync(customerId, cancellationToken);
        var invoices = await dbContext.Invoices
            .Where(invoice => invoice.CustomerId == customerId && invoice.PaymentStatus != PaymentStatus.Void && invoice.BalanceAmount > 0)
            .ToListAsync(cancellationToken);
        var lastPayment = await dbContext.Payments
            .Where(payment => payment.CustomerId == customerId && payment.PaymentKind == PaymentKind.Payment)
            .OrderByDescending(payment => payment.PaymentDate)
            .Select(payment => (DateTimeOffset?)payment.PaymentDate)
            .FirstOrDefaultAsync(cancellationToken);

        return new CreditPanelSnapshot(
            customerId,
            account?.OutstandingBalance ?? invoices.Sum(invoice => invoice.BalanceAmount),
            account?.CreditLimit,
            invoices.Count(invoice => invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.UtcNow.Date),
            lastPayment);
    }

    public async Task<int> GetNextInvoiceSequenceAsync(DateOnly invoiceDate, SaleChannel saleChannel, CancellationToken cancellationToken = default)
    {
        var start = invoiceDate.ToDateTime(TimeOnly.MinValue);
        var end = invoiceDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var count = await dbContext.Invoices
            .CountAsync(invoice =>
                invoice.SaleChannel == saleChannel &&
                invoice.InvoiceDate >= start &&
                invoice.InvoiceDate < end,
                cancellationToken);

        return count + 1;
    }

    public async Task AddInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await dbContext.Invoices.AddAsync(invoice, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await dbContext.Payments.AddAsync(payment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default)
    {
        await dbContext.SalesReturns.AddAsync(salesReturn, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddWastageRecordAsync(WastageRecord wastageRecord, CancellationToken cancellationToken = default)
    {
        await dbContext.WastageRecords.AddAsync(wastageRecord, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> GetPreviouslyReturnedQuantityAsync(Guid invoiceItemId, CancellationToken cancellationToken = default)
    {
        var quantities = await dbContext.SalesReturnItems
            .Where(item => item.InvoiceItemId == invoiceItemId)
            .Select(item => item.Quantity)
            .ToListAsync(cancellationToken);

        return quantities.Sum();
    }

    public async Task UpdateBatchAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        dbContext.Batches.Update(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default)
    {
        dbContext.BusinessAccounts.Update(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        return dbContext.Invoices
            .Include(invoice => invoice.Items)
            .SingleOrDefaultAsync(invoice => invoice.InvoiceId == invoiceId, cancellationToken);
    }

    public async Task UpdateInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        dbContext.Invoices.Update(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ProductCategory?> GetProductCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return dbContext.ProductCategories.SingleOrDefaultAsync(category => category.CategoryId == categoryId, cancellationToken);
    }

    public Task<ProductCategory?> GetProductCategoryByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return dbContext.ProductCategories.SingleOrDefaultAsync(category => category.Name == name, cancellationToken);
    }

    public async Task AddProductCategoryAsync(ProductCategory category, CancellationToken cancellationToken = default)
    {
        await dbContext.ProductCategories.AddAsync(category, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddProductAsync(Product product, CancellationToken cancellationToken = default)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddBatchAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        await dbContext.Batches.AddAsync(batch, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> GetBatchesByProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Batches
            .Where(batch => batch.ProductId == productId)
            .OrderBy(batch => batch.PurchaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> GetAllBatchesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Batches.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetSoldQuantityAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var quantities = await dbContext.InvoiceItems
            .Where(item => item.BatchId == batchId)
            .Join(
                dbContext.Invoices.Where(invoice => invoice.PaymentStatus != PaymentStatus.Void),
                item => item.InvoiceId,
                invoice => invoice.InvoiceId,
                (item, _) => item.Quantity)
            .ToListAsync(cancellationToken);

        return quantities.Sum();
    }

    public async Task<decimal> GetWastedQuantityAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var quantities = await dbContext.WastageRecords
            .Where(record => record.BatchId == batchId)
            .Select(record => record.Quantity)
            .ToListAsync(cancellationToken);

        return quantities.Sum();
    }

    public async Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddBusinessAccountAsync(BusinessAccount account, CancellationToken cancellationToken = default)
    {
        await dbContext.BusinessAccounts.AddAsync(account, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetOutstandingInvoicesAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .Where(invoice => invoice.CustomerId == customerId && invoice.PaymentStatus != PaymentStatus.Void && invoice.BalanceAmount > 0)
            .OrderBy(invoice => invoice.InvoiceDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetAllOutstandingCreditInvoicesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .Where(invoice =>
                invoice.CustomerId.HasValue &&
                invoice.PaymentStatus != PaymentStatus.Void &&
                invoice.BalanceAmount > 0 &&
                (invoice.PaymentMethod == PaymentMethod.Credit || invoice.PaymentMethod == PaymentMethod.Mixed))
            .OrderBy(invoice => invoice.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerPaymentRecord>> GetPaymentHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .Where(payment => payment.CustomerId == customerId)
            .Join(
                dbContext.Invoices,
                payment => payment.InvoiceId,
                invoice => invoice.InvoiceId,
                (payment, invoice) => new CustomerPaymentRecord(
                    invoice.InvoiceNumber,
                    payment.PaymentDate,
                    payment.Amount,
                    invoice.PaymentStatus))
            .OrderByDescending(record => record.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task AddNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await dbContext.Notifications.AddAsync(notification, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Update(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetNotificationLogAsync(
        Guid? customerId,
        Guid? invoiceId,
        NotificationType? notificationType,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .Where(notification => !customerId.HasValue || notification.CustomerId == customerId)
            .Where(notification => !invoiceId.HasValue || notification.InvoiceId == invoiceId)
            .Where(notification => !notificationType.HasValue || notification.NotificationType == notificationType)
            .OrderByDescending(notification => notification.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasNotificationAsync(
        Guid? customerId,
        Guid? invoiceId,
        NotificationType notificationType,
        DateOnly? scheduledDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications
            .Where(notification => notification.CustomerId == customerId)
            .Where(notification => notification.InvoiceId == invoiceId)
            .Where(notification => notification.NotificationType == notificationType);

        if (scheduledDate.HasValue)
        {
            var start = scheduledDate.Value.ToDateTime(TimeOnly.MinValue);
            var end = scheduledDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(notification => notification.ScheduledAt >= start && notification.ScheduledAt < end);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetFailedNotificationsDueForRetryAsync(
        DateTimeOffset asOf,
        int maxRetryCount,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .Where(notification => notification.Status == NotificationStatus.Failed)
            .Where(notification => notification.RetryCount < maxRetryCount)
            .Where(notification => !notification.NextRetryAt.HasValue || notification.NextRetryAt <= asOf)
            .ToListAsync(cancellationToken);
    }

    public Task<NotificationTemplate?> GetNotificationTemplateAsync(NotificationType notificationType, CancellationToken cancellationToken = default)
    {
        return dbContext.NotificationTemplates.SingleOrDefaultAsync(template => template.NotificationType == notificationType, cancellationToken);
    }

    public async Task UpsertNotificationTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        var existingTemplate = await dbContext.NotificationTemplates
            .SingleOrDefaultAsync(item => item.NotificationType == template.NotificationType, cancellationToken);
        if (existingTemplate is null)
        {
            await dbContext.NotificationTemplates.AddAsync(template, cancellationToken);
        }
        else
        {
            existingTemplate.TemplateBody = template.TemplateBody;
            existingTemplate.UpdatedBy = template.UpdatedBy;
            existingTemplate.UpdatedAt = template.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetOutstandingCreditInvoicesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllOutstandingCreditInvoicesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesForDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var start = businessDate.ToDateTime(TimeOnly.MinValue);
        var end = businessDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await dbContext.Invoices
            .Include(invoice => invoice.Items)
            .Where(invoice => invoice.InvoiceDate >= start && invoice.InvoiceDate < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WastageRecord>> GetWastageRecordsForDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        return await dbContext.WastageRecords
            .Where(record => record.WastageDate.Date == businessDate.ToDateTime(TimeOnly.MinValue).Date)
            .ToListAsync(cancellationToken);
    }

    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
    }

    public Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.SingleOrDefaultAsync(user => user.Username == username, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetUsersByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .Where(user => user.Role == role)
            .OrderBy(user => user.Username)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.AnyAsync(user => user.Username == username, cancellationToken);
    }

    public async Task AddUserAsync(User user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
