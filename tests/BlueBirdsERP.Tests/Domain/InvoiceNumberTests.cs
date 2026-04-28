using BlueBirdsERP.Domain.BusinessRules;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class InvoiceNumberTests
{
    [Fact]
    public void Wholesale_invoice_number_uses_w_channel_code()
    {
        var invoiceNumber = PoultryBusinessRules.CreateInvoiceNumber(
            new DateOnly(2026, 4, 28),
            SaleChannel.Wholesale,
            1);

        Assert.Equal("INV-20260428W-0001", invoiceNumber);
    }

    [Fact]
    public void Retail_invoice_number_uses_r_channel_code()
    {
        var invoiceNumber = PoultryBusinessRules.CreateInvoiceNumber(
            new DateOnly(2026, 4, 28),
            SaleChannel.Retail,
            42);

        Assert.Equal("INV-20260428R-0042", invoiceNumber);
    }
}

