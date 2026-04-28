using BlueBirdsERP.Application.Abstractions;

namespace BlueBirdsERP.Infrastructure.Printing;

public sealed class WindowsReceiptPrinter : IReceiptPrinter
{
    public Task PrintInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

