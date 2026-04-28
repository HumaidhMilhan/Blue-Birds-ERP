using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Domain.BusinessRules;

public static class PoultryBusinessRules
{
    public static bool IsPaymentMethodAllowed(SaleChannel saleChannel, PaymentMethod paymentMethod)
    {
        return saleChannel switch
        {
            SaleChannel.Retail => paymentMethod is PaymentMethod.Cash or PaymentMethod.Card,
            SaleChannel.Wholesale => paymentMethod is PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Credit or PaymentMethod.Mixed,
            _ => false
        };
    }

    public static bool CanDeductStock(decimal remainingQuantity, decimal requestedQuantity)
    {
        return requestedQuantity > 0 && remainingQuantity >= requestedQuantity;
    }

    public static void EnsureCanDeductStock(Batch batch, decimal requestedQuantity)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (!CanDeductStock(batch.RemainingQuantity, requestedQuantity))
        {
            throw new InvalidOperationException("Batch stock cannot be deducted below zero.");
        }
    }

    public static WastageRecord CreateCustomerReturnWastage(
        SalesReturn salesReturn,
        InvoiceItem returnedItem,
        decimal returnedQuantity,
        decimal batchCostPrice)
    {
        ArgumentNullException.ThrowIfNull(salesReturn);
        ArgumentNullException.ThrowIfNull(returnedItem);

        if (returnedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returnedQuantity), "Returned quantity must be greater than zero.");
        }

        return new WastageRecord
        {
            BatchId = returnedItem.BatchId,
            ProductId = returnedItem.ProductId,
            WastageDate = salesReturn.ReturnDate.Date,
            Quantity = returnedQuantity,
            WastageType = WastageType.CustomerReturn,
            RelatedReturnId = salesReturn.ReturnId,
            EstimatedLoss = returnedQuantity * batchCostPrice,
            RecordedBy = salesReturn.ProcessedBy,
            Notes = "Generated from customer sales return."
        };
    }

    public static CreditLimitAlert EvaluateCreditLimit(BusinessAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var isTriggered = account.CreditLimit > 0 && account.OutstandingBalance >= account.CreditLimit;
        var availableCredit = account.CreditLimit - account.OutstandingBalance;

        return new CreditLimitAlert(
            IsTriggered: isTriggered,
            IsBlocking: false,
            OutstandingBalance: account.OutstandingBalance,
            AvailableCredit: availableCredit);
    }
}

public sealed record CreditLimitAlert(
    bool IsTriggered,
    bool IsBlocking,
    decimal OutstandingBalance,
    decimal AvailableCredit);

