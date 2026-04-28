using BlueBirdsERP.Domain.BusinessRules;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class PoultryBusinessRulesTests
{
    [Theory]
    [InlineData(PaymentMethod.Cash, true)]
    [InlineData(PaymentMethod.Card, true)]
    [InlineData(PaymentMethod.Credit, false)]
    [InlineData(PaymentMethod.Mixed, false)]
    public void Retail_checkout_allows_cash_and_card_only(PaymentMethod paymentMethod, bool expected)
    {
        var actual = PoultryBusinessRules.IsPaymentMethodAllowed(SaleChannel.Retail, paymentMethod);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.Card)]
    [InlineData(PaymentMethod.Credit)]
    [InlineData(PaymentMethod.Mixed)]
    public void Wholesale_checkout_allows_all_srs_payment_methods(PaymentMethod paymentMethod)
    {
        var actual = PoultryBusinessRules.IsPaymentMethodAllowed(SaleChannel.Wholesale, paymentMethod);

        Assert.True(actual);
    }

    [Fact]
    public void Batch_stock_cannot_be_deducted_below_zero()
    {
        var batch = new Batch
        {
            RemainingQuantity = 4.5m
        };

        Assert.False(PoultryBusinessRules.CanDeductStock(batch.RemainingQuantity, 4.6m));
        Assert.Throws<InvalidOperationException>(() => PoultryBusinessRules.EnsureCanDeductStock(batch, 4.6m));
    }

    [Fact]
    public void Customer_return_creates_wastage_without_restocking_batch()
    {
        var batchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var processorId = Guid.NewGuid();
        var batch = new Batch
        {
            BatchId = batchId,
            ProductId = productId,
            RemainingQuantity = 12m,
            CostPrice = 900m
        };
        var returnedItem = new InvoiceItem
        {
            ProductId = productId,
            BatchId = batchId,
            Quantity = 2m
        };
        var salesReturn = new SalesReturn
        {
            ProcessedBy = processorId,
            ReturnDate = new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero)
        };

        var wastage = PoultryBusinessRules.CreateCustomerReturnWastage(salesReturn, returnedItem, 2m, batch.CostPrice);

        Assert.Equal(WastageType.CustomerReturn, wastage.WastageType);
        Assert.Equal(salesReturn.ReturnId, wastage.RelatedReturnId);
        Assert.Equal(batchId, wastage.BatchId);
        Assert.Equal(productId, wastage.ProductId);
        Assert.Equal(1800m, wastage.EstimatedLoss);
        Assert.Equal(12m, batch.RemainingQuantity);
    }

    [Fact]
    public void Credit_limit_alert_is_high_priority_but_non_blocking()
    {
        var account = new BusinessAccount
        {
            CreditLimit = 100_000m,
            OutstandingBalance = 125_000m
        };

        var alert = PoultryBusinessRules.EvaluateCreditLimit(account);

        Assert.True(alert.IsTriggered);
        Assert.False(alert.IsBlocking);
        Assert.Equal(125_000m, alert.OutstandingBalance);
        Assert.Equal(-25_000m, alert.AvailableCredit);
    }
}

