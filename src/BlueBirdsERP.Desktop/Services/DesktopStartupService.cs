using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Configuration;
using BlueBirdsERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BlueBirdsERP.Desktop.Services;

public sealed class DesktopStartupService(
    PoultryProDbContext dbContext,
    InfrastructureOptions options,
    IApplicationBootstrapService bootstrapService,
    IInventoryService inventoryService,
    ICustomerAccountService customerAccountService,
    IPOSCheckoutService checkoutService)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await bootstrapService.EnsureDevelopmentBootstrapAsync(cancellationToken);

        if (!string.Equals(options.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            await SeedDevelopmentDataAsync(cancellationToken);
        }
    }

    private async Task SeedDevelopmentDataAsync(CancellationToken cancellationToken)
    {
        var admin = await dbContext.Users
            .Where(user => user.Role == UserRole.Admin && user.IsActive)
            .OrderBy(user => user.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            return;
        }

        var category = await dbContext.ProductCategories
            .SingleOrDefaultAsync(item => item.Name == "Poultry", cancellationToken);

        if (category is null)
        {
            var categoryResult = await inventoryService.CreateProductCategoryAsync(
                new CreateProductCategoryRequest("Poultry", admin.UserId, admin.Role),
                cancellationToken);

            category = await dbContext.ProductCategories
                .SingleAsync(item => item.CategoryId == categoryResult.CategoryId, cancellationToken);
        }

        await EnsureProductWithBatchAsync(
            category.CategoryId,
            "Whole Chicken",
            PricingType.WeightBased,
            "Kg",
            sellingPrice: 1180m,
            reorderLevel: 10m,
            initialQuantity: 42m,
            costPrice: 920m,
            admin.UserId,
            admin.Role,
            cancellationToken);

        await EnsureProductWithBatchAsync(
            category.CategoryId,
            "Chicken Breast",
            PricingType.WeightBased,
            "Kg",
            sellingPrice: 1520m,
            reorderLevel: 8m,
            initialQuantity: 18m,
            costPrice: 1160m,
            admin.UserId,
            admin.Role,
            cancellationToken);

        await EnsureProductWithBatchAsync(
            category.CategoryId,
            "Chicken Wings",
            PricingType.WeightBased,
            "Kg",
            sellingPrice: 980m,
            reorderLevel: 6m,
            initialQuantity: 12m,
            costPrice: 710m,
            admin.UserId,
            admin.Role,
            cancellationToken);

        await EnsureProductWithBatchAsync(
            category.CategoryId,
            "Fresh Eggs",
            PricingType.UnitBased,
            "pieces",
            sellingPrice: 62m,
            reorderLevel: 120m,
            initialQuantity: 620m,
            costPrice: 43m,
            admin.UserId,
            admin.Role,
            cancellationToken);

        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(item => item.Name == "ABC Hotels", cancellationToken);

        if (customer is null)
        {
            var result = await customerAccountService.CreateBusinessAccountAsync(
                new CreateBusinessAccountRequest(
                    "ABC Hotels",
                    "+94770000001",
                    "+94770000001",
                    "accounts@abchotels.example",
                    "Colombo",
                    150000m,
                    7,
                    2,
                    admin.UserId,
                    admin.Role),
                cancellationToken);

            customer = await dbContext.Customers.SingleAsync(item => item.CustomerId == result.CustomerId, cancellationToken);
        }

        if (!await dbContext.Invoices.AnyAsync(cancellationToken))
        {
            await SeedInvoicesAsync(admin.UserId, customer.CustomerId, cancellationToken);
        }
    }

    private async Task EnsureProductWithBatchAsync(
        Guid categoryId,
        string name,
        PricingType pricingType,
        string unit,
        decimal sellingPrice,
        decimal reorderLevel,
        decimal initialQuantity,
        decimal costPrice,
        Guid adminUserId,
        UserRole adminRole,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (product is null)
        {
            var created = await inventoryService.CreateProductAsync(
                new CreateProductRequest(categoryId, name, pricingType, unit, sellingPrice, reorderLevel, adminUserId, adminRole),
                cancellationToken);

            product = await dbContext.Products.SingleAsync(item => item.ProductId == created.ProductId, cancellationToken);
        }

        var hasActiveBatch = await dbContext.Batches.AnyAsync(
            batch => batch.ProductId == product.ProductId && batch.RemainingQuantity > 0,
            cancellationToken);

        if (!hasActiveBatch)
        {
            await inventoryService.RecordManualBatchPurchaseAsync(
                new ManualBatchPurchaseRequest(
                    product.ProductId,
                    DateTime.Today,
                    DateTime.Today.AddDays(5),
                    initialQuantity,
                    costPrice,
                    adminUserId,
                    adminRole),
                cancellationToken);
        }
    }

    private async Task SeedInvoicesAsync(Guid adminUserId, Guid businessCustomerId, CancellationToken cancellationToken)
    {
        var wholeChicken = await dbContext.Products.SingleAsync(item => item.Name == "Whole Chicken", cancellationToken);
        var wholeChickenBatch = await dbContext.Batches
            .Where(item => item.ProductId == wholeChicken.ProductId && item.RemainingQuantity > 0)
            .OrderBy(item => item.ExpiryDate)
            .FirstAsync(cancellationToken);

        await checkoutService.CheckoutAsync(
            new CheckoutRequest(
                SaleChannel.Retail,
                null,
                adminUserId,
                PaymentMethod.Cash,
                [new CheckoutLineItem(wholeChicken.ProductId, wholeChickenBatch.BatchId, 1.5m, wholeChicken.SellingPrice, 0m)],
                1.5m * wholeChicken.SellingPrice,
                0m,
                0m,
                null,
                Notes: "Development retail seed invoice"),
            cancellationToken);

        var breast = await dbContext.Products.SingleAsync(item => item.Name == "Chicken Breast", cancellationToken);
        var breastBatch = await dbContext.Batches
            .Where(item => item.ProductId == breast.ProductId && item.RemainingQuantity > 0)
            .OrderBy(item => item.ExpiryDate)
            .FirstAsync(cancellationToken);
        var wholesaleTotal = 3m * breast.SellingPrice;

        await checkoutService.CheckoutAsync(
            new CheckoutRequest(
                SaleChannel.Wholesale,
                businessCustomerId,
                adminUserId,
                PaymentMethod.Credit,
                [new CheckoutLineItem(breast.ProductId, breastBatch.BatchId, 3m, breast.SellingPrice, 0m)],
                CashAmount: 0m,
                CardAmount: 0m,
                CreditAmount: wholesaleTotal,
                ManualDueDate: DateTime.Today.AddDays(7),
                Notes: "Development wholesale seed invoice"),
            cancellationToken);
    }
}
