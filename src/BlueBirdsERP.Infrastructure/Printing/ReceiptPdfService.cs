using System.Globalization;
using System.Text;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Configuration;
using BlueBirdsERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Infrastructure.Printing;

public sealed class ReceiptPdfService(
    PoultryProDbContext dbContext,
    InfrastructureOptions options) : IReceiptPdfService
{
    public async Task<ReceiptPdfResult> GenerateInvoiceReceiptPdfAsync(
        ReceiptPdfRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role is not (UserRole.Admin or UserRole.Cashier))
        {
            throw new InvalidOperationException("Only Admin and Cashier users can generate receipt previews.");
        }

        var invoice = await dbContext.Invoices
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.InvoiceId == request.InvoiceId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice was not found.");
        var customer = invoice.CustomerId.HasValue
            ? await dbContext.Customers.SingleOrDefaultAsync(item => item.CustomerId == invoice.CustomerId, cancellationToken)
            : null;
        var returns = await dbContext.SalesReturns
            .Where(item => item.InvoiceId == invoice.InvoiceId)
            .ToListAsync(cancellationToken);
        var payments = await dbContext.Payments
            .Where(item => item.InvoiceId == invoice.InvoiceId)
            .OrderBy(item => item.PaymentDate)
            .ToListAsync(cancellationToken);
        var settings = await dbContext.SystemSettings.ToDictionaryAsync(item => item.SettingKey, cancellationToken);
        var companyName = GetSetting(settings, "Receipt.CompanyName", options.Receipt.CompanyName);
        var header = GetSetting(settings, "Receipt.Header", options.Receipt.Header);
        var footer = GetSetting(settings, "Receipt.Footer", options.Receipt.Footer);

        var lines = new List<string>
        {
            companyName,
            header,
            string.Empty,
            $"Invoice: {invoice.InvoiceNumber}",
            $"Date: {invoice.InvoiceDate:yyyy-MM-dd HH:mm}",
            $"Channel: {invoice.SaleChannel}",
            customer is null ? "Customer: Walk-in" : $"Customer: {customer.Name}",
            string.Empty
        };

        foreach (var item in invoice.Items)
        {
            lines.Add($"{item.ProductName} {FormatQty(item.Quantity)} {item.UnitOfMeasure}");
            lines.Add($"  {FormatMoney(item.UnitPrice)}  Disc {FormatMoney(item.DiscountAmount)}  {FormatMoney(item.LineTotal)}");
        }

        lines.Add(string.Empty);
        lines.Add($"Subtotal: {FormatMoney(invoice.Subtotal)}");
        lines.Add($"Discount: {FormatMoney(invoice.DiscountTotal)}");
        lines.Add($"Total: {FormatMoney(invoice.GrandTotal)}");
        lines.Add($"Paid: {FormatMoney(invoice.PaidAmount)}");
        lines.Add($"Balance: {FormatMoney(invoice.BalanceAmount)}");
        if (invoice.RefundedAmount > 0)
        {
            lines.Add($"Refunded: {FormatMoney(invoice.RefundedAmount)}");
        }

        foreach (var payment in payments)
        {
            lines.Add($"{payment.PaymentKind} {payment.PaymentMethod}: {FormatMoney(payment.Amount)}");
        }

        foreach (var salesReturn in returns)
        {
            lines.Add($"Return: {FormatMoney(salesReturn.TotalValue)} Refund {FormatMoney(salesReturn.RefundAmount)}");
        }

        if (invoice.DueDate.HasValue)
        {
            lines.Add($"Due: {invoice.DueDate:yyyy-MM-dd}");
        }

        lines.Add(string.Empty);
        lines.Add(footer);

        var bytes = BuildSimplePdf(lines, options.Receipt.WidthMillimeters);
        return new ReceiptPdfResult(
            invoice.InvoiceId,
            invoice.InvoiceNumber,
            $"{invoice.InvoiceNumber}.pdf",
            bytes);
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<string> lines, int widthMillimeters)
    {
        var widthPoints = Math.Max(200, widthMillimeters * 72m / 25.4m);
        var heightPoints = Math.Max(360, 48 + lines.Count * 13);
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 8 Tf");
        content.AppendLine("11 TL");
        content.AppendLine($"1 0 0 1 10 {(heightPoints - 20).ToString("0.##", CultureInfo.InvariantCulture)} Tm");

        var firstLine = true;
        foreach (var line in lines)
        {
            if (!firstLine)
            {
                content.AppendLine("T*");
            }

            content.AppendLine($"({EscapePdf(line)}) Tj");
            firstLine = false;
        }

        content.AppendLine("ET");
        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {widthPoints.ToString("0.##", CultureInfo.InvariantCulture)} {heightPoints.ToString("0.##", CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>",
            $"<< /Length {contentBytes.Length} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder();
        var offsets = new List<int> { 0 };
        pdf.AppendLine("%PDF-1.4");
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine($"{index + 1} 0 obj");
            pdf.AppendLine(objects[index]);
            pdf.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string GetSetting(
        IReadOnlyDictionary<string, SystemSetting> settings,
        string key,
        string fallback)
    {
        return settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.SettingValue)
            ? setting.SettingValue
            : fallback;
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatQty(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string EscapePdf(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
