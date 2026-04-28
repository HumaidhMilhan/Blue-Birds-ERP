using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.BusinessRules;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Application.Inventory;

public sealed class InventoryService(
    IInventoryDataStore dataStore,
    ITransactionRunner transactionRunner,
    IAuditLogger auditLogger,
    ISystemClock clock) : IInventoryService
{
    public async Task<ProductCategoryResult> CreateProductCategoryAsync(
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateRequired(request.Name, "Product category name is required.");

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var existing = await dataStore.GetProductCategoryByNameAsync(request.Name.Trim(), token);
            if (existing is not null)
            {
                throw new InventoryValidationException("Product category already exists.");
            }

            var category = new ProductCategory
            {
                Name = request.Name.Trim(),
                IsActive = true
            };

            await dataStore.AddProductCategoryAsync(category, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.CreatedBy,
                request.Role,
                "PRODUCT_CATEGORY_CREATE",
                "INVENTORY",
                nameof(ProductCategory),
                category.CategoryId,
                null,
                $"{{\"name\":\"{category.Name}\"}}"), token);

            return new ProductCategoryResult(category.CategoryId, category.Name, category.IsActive);
        }, cancellationToken);
    }

    public async Task<ProductCatalogItem> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateRequired(request.Name, "Product name is required.");
        ValidateProductNumbers(request.SellingPrice, request.ReorderLevel);

        if (!InventoryBusinessRules.IsUnitAllowed(request.PricingType, request.UnitOfMeasure))
        {
            throw new InventoryValidationException("Unit is not allowed for the selected pricing type.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var category = await dataStore.GetProductCategoryAsync(request.CategoryId, token)
                ?? throw new InventoryValidationException("Product category was not found.");

            var existing = await dataStore.GetProductByNameAsync(request.Name.Trim(), token);
            if (existing is not null)
            {
                throw new InventoryValidationException("Product already exists.");
            }

            var product = new Product
            {
                CategoryId = category.CategoryId,
                Name = request.Name.Trim(),
                PricingType = request.PricingType,
                UnitOfMeasure = InventoryBusinessRules.NormalizeUnit(request.PricingType, request.UnitOfMeasure),
                SellingPrice = request.SellingPrice,
                ReorderLevel = request.ReorderLevel,
                IsActive = true
            };

            await dataStore.AddProductAsync(product, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.CreatedBy,
                request.Role,
                "PRODUCT_CREATE",
                "INVENTORY",
                nameof(Product),
                product.ProductId,
                null,
                $"{{\"name\":\"{product.Name}\",\"unit\":\"{product.UnitOfMeasure}\"}}"), token);

            return CreateProductCatalogItem(product);
        }, cancellationToken);
    }

    public async Task<BatchResult> RecordManualBatchPurchaseAsync(
        ManualBatchPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateQuantity(request.InitialQuantity, "Initial quantity must be greater than zero.");

        if (request.CostPrice < 0)
        {
            throw new InventoryValidationException("Cost price cannot be negative.");
        }

        return await transactionRunner.ExecuteAsync(async token =>
        {
            _ = await dataStore.GetProductAsync(request.ProductId, token)
                ?? throw new InventoryValidationException("Product was not found.");

            var batch = new Batch
            {
                ProductId = request.ProductId,
                PurchaseDate = request.PurchaseDate.Date,
                ExpiryDate = request.ExpiryDate?.Date,
                InitialQuantity = request.InitialQuantity,
                RemainingQuantity = request.InitialQuantity,
                CostPrice = request.CostPrice,
                Status = BatchStatus.Active
            };

            await dataStore.AddBatchAsync(batch, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.RecordedBy,
                request.Role,
                "MANUAL_BATCH_PURCHASE_CREATE",
                "INVENTORY",
                nameof(Batch),
                batch.BatchId,
                null,
                $"{{\"productId\":\"{batch.ProductId}\",\"quantity\":{batch.InitialQuantity}}}"), token);

            return CreateBatchResult(batch);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Batch>> GetAvailableBatchesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new InventoryValidationException("Product is required.");
        }

        var today = clock.Now.Date;
        var batches = await dataStore.GetBatchesByProductAsync(productId, cancellationToken);
        return batches
            .Where(batch => IsActiveNonExpiredBatch(batch, today))
            .OrderBy(batch => batch.PurchaseDate)
            .ThenBy(batch => batch.ExpiryDate)
            .ToList();
    }

    public async Task DeductBatchStockAsync(
        Guid batchId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        ValidateQuantity(quantity, "Quantity must be greater than zero.");

        await transactionRunner.ExecuteAsync(async token =>
        {
            var batch = await dataStore.GetBatchAsync(batchId, token)
                ?? throw new InventoryValidationException("Batch was not found.");

            PoultryBusinessRules.EnsureCanDeductStock(batch, quantity);
            batch.RemainingQuantity -= quantity;
            if (batch.RemainingQuantity == 0)
            {
                batch.Status = BatchStatus.Exhausted;
            }

            await dataStore.UpdateBatchAsync(batch, token);
            return true;
        }, cancellationToken);
    }

    public async Task<WastageRecord> RecordWastageAsync(
        RecordWastageRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(request.Role);
        ValidateQuantity(request.Quantity, "Wastage quantity must be greater than zero.");

        return await transactionRunner.ExecuteAsync(async token =>
        {
            var batch = await dataStore.GetBatchAsync(request.BatchId, token)
                ?? throw new InventoryValidationException("Batch was not found.");

            PoultryBusinessRules.EnsureCanDeductStock(batch, request.Quantity);
            batch.RemainingQuantity -= request.Quantity;
            if (batch.RemainingQuantity == 0)
            {
                batch.Status = BatchStatus.Exhausted;
            }

            var wastage = new WastageRecord
            {
                BatchId = batch.BatchId,
                ProductId = batch.ProductId,
                WastageDate = request.WastageDate.Date,
                Quantity = request.Quantity,
                WastageType = request.WastageType,
                EstimatedLoss = request.Quantity * batch.CostPrice,
                Notes = request.Notes,
                RecordedBy = request.RecordedBy
            };

            await dataStore.UpdateBatchAsync(batch, token);
            await dataStore.AddWastageRecordAsync(wastage, token);
            await auditLogger.WriteAsync(new AuditEntry(
                request.RecordedBy,
                request.Role,
                "BATCH_WASTAGE_RECORD",
                "INVENTORY",
                nameof(WastageRecord),
                wastage.WastageId,
                null,
                $"{{\"batchId\":\"{batch.BatchId}\",\"quantity\":{wastage.Quantity},\"type\":\"{wastage.WastageType}\"}}"), token);

            return wastage;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductStockLevel>> GetProductStockLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        var today = clock.Now.Date;
        var products = await dataStore.GetActiveProductsAsync(cancellationToken);
        var stockLevels = new List<ProductStockLevel>();

        foreach (var product in products)
        {
            var batches = await dataStore.GetBatchesByProductAsync(product.ProductId, cancellationToken);
            var remaining = batches
                .Where(batch => IsActiveNonExpiredBatch(batch, today))
                .Sum(batch => batch.RemainingQuantity);

            stockLevels.Add(new ProductStockLevel(
                product.ProductId,
                product.Name,
                product.UnitOfMeasure,
                remaining,
                product.ReorderLevel));
        }

        return stockLevels
            .OrderBy(level => level.ProductName)
            .ToList();
    }

    public async Task<IReadOnlyList<InventoryAlert>> GetInventoryAlertsAsync(
        DateOnly asOfDate,
        int nearExpiryThresholdDays,
        CancellationToken cancellationToken = default)
    {
        if (nearExpiryThresholdDays < 0)
        {
            throw new InventoryValidationException("Near-expiry threshold cannot be negative.");
        }

        var alerts = new List<InventoryAlert>();
        var products = await dataStore.GetActiveProductsAsync(cancellationToken);
        var allBatches = await dataStore.GetAllBatchesAsync(cancellationToken);

        foreach (var product in products)
        {
            var productBatches = allBatches.Where(batch => batch.ProductId == product.ProductId).ToList();
            var activeStock = productBatches
                .Where(batch => IsActiveNonExpiredBatch(batch, asOfDate.ToDateTime(TimeOnly.MinValue)))
                .Sum(batch => batch.RemainingQuantity);

            if (activeStock < product.ReorderLevel)
            {
                alerts.Add(new InventoryAlert(
                    "LOW_STOCK",
                    product.ProductId,
                    product.Name,
                    BatchId: null,
                    $"Stock is {activeStock} {product.UnitOfMeasure}, below reorder level {product.ReorderLevel}."));
            }

            foreach (var batch in productBatches.Where(batch => IsNearExpiry(batch, asOfDate, nearExpiryThresholdDays)))
            {
                alerts.Add(new InventoryAlert(
                    "NEAR_EXPIRY",
                    product.ProductId,
                    product.Name,
                    batch.BatchId,
                    $"Batch {CreateBatchReference(batch.BatchId)} expires on {batch.ExpiryDate:yyyy-MM-dd}."));
            }
        }

        return alerts;
    }

    public async Task<IReadOnlyList<BatchHistoryEntry>> GetBatchHistoryAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var product = await dataStore.GetProductAsync(productId, cancellationToken)
            ?? throw new InventoryValidationException("Product was not found.");
        var batches = await dataStore.GetBatchesByProductAsync(productId, cancellationToken);
        var history = new List<BatchHistoryEntry>();

        foreach (var batch in batches.OrderBy(batch => batch.PurchaseDate))
        {
            var soldQuantity = await dataStore.GetSoldQuantityAsync(batch.BatchId, cancellationToken);
            var wastedQuantity = await dataStore.GetWastedQuantityAsync(batch.BatchId, cancellationToken);

            history.Add(new BatchHistoryEntry(
                batch.BatchId,
                product.ProductId,
                product.Name,
                batch.PurchaseDate,
                batch.ExpiryDate,
                batch.InitialQuantity,
                soldQuantity,
                wastedQuantity,
                batch.RemainingQuantity,
                batch.Status));
        }

        return history;
    }

    private static ProductCatalogItem CreateProductCatalogItem(Product product)
    {
        return new ProductCatalogItem(
            product.ProductId,
            product.CategoryId,
            product.Name,
            product.PricingType,
            product.UnitOfMeasure,
            product.SellingPrice,
            product.ReorderLevel,
            product.IsActive);
    }

    private static BatchResult CreateBatchResult(Batch batch)
    {
        return new BatchResult(
            batch.BatchId,
            batch.ProductId,
            batch.PurchaseDate,
            batch.ExpiryDate,
            batch.InitialQuantity,
            batch.RemainingQuantity,
            batch.CostPrice,
            batch.Status);
    }

    private static bool IsActiveNonExpiredBatch(Batch batch, DateTime asOfDate)
    {
        return batch.Status == BatchStatus.Active &&
            batch.RemainingQuantity > 0 &&
            (!batch.ExpiryDate.HasValue || batch.ExpiryDate.Value.Date >= asOfDate.Date);
    }

    private static bool IsNearExpiry(Batch batch, DateOnly asOfDate, int thresholdDays)
    {
        if (batch.Status != BatchStatus.Active || batch.RemainingQuantity <= 0 || !batch.ExpiryDate.HasValue)
        {
            return false;
        }

        var expiry = DateOnly.FromDateTime(batch.ExpiryDate.Value.Date);
        var daysUntilExpiry = expiry.DayNumber - asOfDate.DayNumber;
        return daysUntilExpiry >= 0 && daysUntilExpiry <= thresholdDays;
    }

    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new InventoryValidationException("Only Admin users can manage inventory.");
        }
    }

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InventoryValidationException(message);
        }
    }

    private static void ValidateProductNumbers(decimal sellingPrice, decimal reorderLevel)
    {
        if (sellingPrice < 0)
        {
            throw new InventoryValidationException("Selling price cannot be negative.");
        }

        if (reorderLevel < 0)
        {
            throw new InventoryValidationException("Reorder level cannot be negative.");
        }
    }

    private static void ValidateQuantity(decimal quantity, string message)
    {
        if (quantity <= 0)
        {
            throw new InventoryValidationException(message);
        }
    }

    private static string CreateBatchReference(Guid batchId)
    {
        return batchId.ToString("N")[..8].ToUpperInvariant();
    }
}
