using BlueBirdsERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class PoultryProDbContext(DbContextOptions<PoultryProDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BusinessAccount> BusinessAccounts => Set<BusinessAccount>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WastageRecord> WastageRecords => Set<WastageRecord>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasKey(entity => entity.UserId);
        modelBuilder.Entity<Customer>().HasKey(entity => entity.CustomerId);
        modelBuilder.Entity<BusinessAccount>().HasKey(entity => entity.AccountId);
        modelBuilder.Entity<ProductCategory>().HasKey(entity => entity.CategoryId);
        modelBuilder.Entity<Product>().HasKey(entity => entity.ProductId);
        modelBuilder.Entity<Batch>().HasKey(entity => entity.BatchId);
        modelBuilder.Entity<Invoice>().HasKey(entity => entity.InvoiceId);
        modelBuilder.Entity<InvoiceItem>().HasKey(entity => entity.ItemId);
        modelBuilder.Entity<Payment>().HasKey(entity => entity.PaymentId);
        modelBuilder.Entity<WastageRecord>().HasKey(entity => entity.WastageId);
        modelBuilder.Entity<SalesReturn>().HasKey(entity => entity.ReturnId);
        modelBuilder.Entity<SalesReturnItem>().HasKey(entity => entity.ReturnItemId);
        modelBuilder.Entity<Notification>().HasKey(entity => entity.NotificationId);
        modelBuilder.Entity<NotificationTemplate>().HasKey(entity => entity.TemplateId);
        modelBuilder.Entity<AuditLog>().HasKey(entity => entity.LogId);

        modelBuilder.Entity<User>().HasIndex(entity => entity.Username).IsUnique();
        modelBuilder.Entity<BusinessAccount>().HasIndex(entity => entity.CustomerId).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(entity => entity.InvoiceNumber).IsUnique();
        modelBuilder.Entity<NotificationTemplate>().HasIndex(entity => entity.NotificationType).IsUnique();

        ConfigureMoney(modelBuilder);
    }

    private static void ConfigureMoney(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessAccount>(entity =>
        {
            entity.Property(item => item.CreditLimit).HasPrecision(12, 2);
            entity.Property(item => item.OutstandingBalance).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(item => item.SellingPrice).HasPrecision(10, 2);
            entity.Property(item => item.ReorderLevel).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Batch>(entity =>
        {
            entity.Property(item => item.InitialQuantity).HasPrecision(10, 2);
            entity.Property(item => item.RemainingQuantity).HasPrecision(10, 2);
            entity.Property(item => item.CostPrice).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(item => item.Subtotal).HasPrecision(12, 2);
            entity.Property(item => item.DiscountTotal).HasPrecision(12, 2);
            entity.Property(item => item.GrandTotal).HasPrecision(12, 2);
            entity.Property(item => item.PaidAmount).HasPrecision(12, 2);
            entity.Property(item => item.BalanceAmount).HasPrecision(12, 2);
            entity.Property(item => item.RefundedAmount).HasPrecision(12, 2);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.Property(item => item.Quantity).HasPrecision(10, 2);
            entity.Property(item => item.UnitPrice).HasPrecision(10, 2);
            entity.Property(item => item.DiscountAmount).HasPrecision(10, 2);
            entity.Property(item => item.LineTotal).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Payment>().Property(item => item.Amount).HasPrecision(12, 2);
        modelBuilder.Entity<WastageRecord>().Property(item => item.EstimatedLoss).HasPrecision(12, 2);
        modelBuilder.Entity<SalesReturn>(entity =>
        {
            entity.Property(item => item.TotalValue).HasPrecision(12, 2);
            entity.Property(item => item.RefundAmount).HasPrecision(12, 2);
        });

        modelBuilder.Entity<SalesReturnItem>(entity =>
        {
            entity.Property(item => item.Quantity).HasPrecision(10, 2);
            entity.Property(item => item.SoldUnitValue).HasPrecision(10, 2);
            entity.Property(item => item.ReturnValue).HasPrecision(12, 2);
        });
    }
}
