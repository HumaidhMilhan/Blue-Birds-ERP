using BlueBirdsERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class LocalPosDbContext(DbContextOptions<LocalPosDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WastageRecord> WastageRecords => Set<WastageRecord>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>().HasKey(entity => entity.InvoiceId);
        modelBuilder.Entity<InvoiceItem>().HasKey(entity => entity.ItemId);
        modelBuilder.Entity<Payment>().HasKey(entity => entity.PaymentId);
        modelBuilder.Entity<WastageRecord>().HasKey(entity => entity.WastageId);
        modelBuilder.Entity<SalesReturn>().HasKey(entity => entity.ReturnId);
        modelBuilder.Entity<SalesReturnItem>().HasKey(entity => entity.ReturnItemId);

        modelBuilder.Entity<Invoice>().HasIndex(entity => entity.InvoiceNumber).IsUnique();

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
