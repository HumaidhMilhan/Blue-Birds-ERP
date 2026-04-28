using BlueBirdsERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Persistence;

public sealed class LocalPosDbContext(DbContextOptions<LocalPosDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WastageRecord> WastageRecords => Set<WastageRecord>();
}

