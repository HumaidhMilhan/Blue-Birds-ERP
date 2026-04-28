using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Application.Inventory;
using BlueBirdsERP.Application.POS;
using BlueBirdsERP.Domain.Entities;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Tests.Domain;

public sealed class InventoryServiceTests
{
    [Fact]
    public async Task Admin_can_create_configurable_categories_and_weight_or_piece_products()
    {
        var harness = CreateHarness();
        var category = await harness.Service.CreateProductCategoryAsync(new CreateProductCategoryRequest(
            "Chicken Parts",
            harness.AdminId,
            UserRole.Admin));

        var weightProduct = await harness.Service.CreateProductAsync(new CreateProductRequest(
            category.CategoryId,
            "Chicken Breast",
            PricingType.WeightBased,
            "kg",
            SellingPrice: 1_000m,
            ReorderLevel: 5m,
            harness.AdminId,
            UserRole.Admin));

        var unitProduct = await harness.Service.CreateProductAsync(new CreateProductRequest(
            category.CategoryId,
            "Egg Pack",
            PricingType.UnitBased,
            "pieces",
            SellingPrice: 60m,
            ReorderLevel: 20m,
            harness.AdminId,
            UserRole.Admin));

        Assert.Equal("Kg", weightProduct.UnitOfMeasure);
        Assert.Equal("pieces", unitProduct.UnitOfMeasure);
        Assert.Equal(["PRODUCT_CATEGORY_CREATE", "PRODUCT_CREATE", "PRODUCT_CREATE"], harness.AuditLogger.Entries.Select(entry => entry.Action));
    }

    [Theory]
    [InlineData(PricingType.WeightBased, "pieces")]
    [InlineData(PricingType.UnitBased, "Kg")]
    [InlineData(PricingType.UnitBased, "g")]
    public async Task Product_creation_rejects_units_not_allowed_for_pricing_type(PricingType pricingType, string unit)
    {
        var harness = CreateHarness();
        var category = harness.Store.AddCategory("Chicken Parts");

        await Assert.ThrowsAsync<InventoryValidationException>(() => harness.Service.CreateProductAsync(new CreateProductRequest(
            category.CategoryId,
            "Invalid Product",
            pricingType,
            unit,
            SellingPrice: 100m,
            ReorderLevel: 1m,
            harness.AdminId,
            UserRole.Admin)));
    }

    [Fact]
    public async Task Manual_batch_purchase_creates_active_batch_directly()
    {
        var harness = CreateHarness();
        var product = harness.Store.AddProduct("Chicken Breast", PricingType.WeightBased, "Kg", sellingPrice: 1_000m, reorderLevel: 5m);

        var result = await harness.Service.RecordManualBatchPurchaseAsync(new ManualBatchPurchaseRequest(
            product.ProductId,
            PurchaseDate: new DateTime(2026, 4, 28),
            ExpiryDate: new DateTime(2026, 5, 2),
            InitialQuantity: 30m,
            CostPrice: 750m,
            harness.AdminId,
            UserRole.Admin));

        var batch = harness.Store.Batches.Single();
        Assert.Equal(result.BatchId, batch.BatchId);
        Assert.Equal(30m, batch.InitialQuantity);
        Assert.Equal(30m, batch.RemainingQuantity);
        Assert.Equal(BatchStatus.Active, batch.Status);
        Assert.Equal("MANUAL_BATCH_PURCHASE_CREATE", harness.AuditLogger.Entries.Single().Action);
    }

    [Fact]
    public async Task Stock_levels_aggregate_active_non_expired_batches_only()
    {
        var harness = CreateHarness();
        var product = harness.Store.AddProduct("Chicken Breast", PricingType.WeightBased, "Kg", sellingPrice: 1_000m, reorderLevel: 10m);
        harness.Store.AddBatch(product.ProductId, remainingQuantity: 5m, expiryDate: new DateTime(2026, 5, 1), status: BatchStatus.Active);
        harness.Store.AddBatch(product.ProductId, remainingQuantity: 7m, expiryDate: null, status: BatchStatus.Active);
        harness.Store.AddBatch(product.ProductId, remainingQuantity: 20m, expiryDate: new DateTime(2026, 4, 1), status: BatchStatus.Active);
        harness.Store.AddBatch(product.ProductId, remainingQuantity: 10m, expiryDate: null, status: BatchStatus.Exhausted);

        var stockLevels = await harness.Service.GetProductStockLevelsAsync();

        Assert.Single(stockLevels);
        Assert.Equal(12m, stockLevels[0].RemainingQuantity);
    }

    [Fact]
    public async Task Wastage_deducts_from_batch_and_records_loss()
    {
        var harness = CreateHarness();
        var product = harness.Store.AddProduct("Chicken Breast", PricingType.WeightBased, "Kg", sellingPrice: 1_000m, reorderLevel: 10m);
        var batch = harness.Store.AddBatch(product.ProductId, remainingQuantity: 5m, costPrice: 700m);

        var wastage = await harness.Service.RecordWastageAsync(new RecordWastageRequest(
            batch.BatchId,
            WastageDate: new DateTime(2026, 4, 28),
            Quantity: 2m,
            WastageType.DamagedPackaging,
            Notes: "Torn pack",
            harness.AdminId,
            UserRole.Admin));

        Assert.Equal(3m, batch.RemainingQuantity);
        Assert.Equal(1_400m, wastage.EstimatedLoss);
        Assert.Equal(WastageType.DamagedPackaging, wastage.WastageType);
        Assert.Equal("BATCH_WASTAGE_RECORD", harness.AuditLogger.Entries.Single().Action);
    }

    [Fact]
    public async Task Wastage_cannot_deduct_batch_below_zero()
    {
        var harness = CreateHarness();
        var product = harness.Store.AddProduct("Chicken Breast", PricingType.WeightBased, "Kg", sellingPrice: 1_000m, reorderLevel: 10m);
        var batch = harness.Store.AddBatch(product.ProductId, remainingQuantity: 1m, costPrice: 700m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.RecordWastageAsync(new RecordWastageRequest(
            batch.BatchId,
            WastageDate: new DateTime(2026, 4, 28),
            Quantity: 2m,
            WastageType.Expiry,
            Notes: null,
            harness.AdminId,
            UserRole.Admin)));

        Assert.Equal(1m, batch.RemainingQuantity);
        Assert.Empty(harness.Store.WastageRecords);
    }

    [Fact]
    public async Task Inventory_alerts_include_low_stock_and_near_expiry()
    {
        var harness = CreateHarness();
        var lowStockProduct = harness.Store.AddProduct("Chicken Breast", PricingType.WeightBased, "Kg", sellingPrice: 1_000m, reorderLevel: 20m);
        var nearExpiryProduct = harness.Store.AddProduct("Chicken Wings", PricingType.WeightBased, "Kg", sellingPrice: 900m, reorderLevel: 1m);
        harness.Store.AddBatch(lowStockProduct.ProductId, remainingQuantity: 5m, expiryDate: null, status: BatchStatus.Active);
        harness.Store.AddBatch(nearExpiryProduct.ProductId, remainingQuantity: 10m, expiryDate: new DateTime(2026, 4, 30), status: BatchStatus.Active);

        var alerts = await harness.Service.GetInventoryAlertsAsync(new DateOnly(2026, 4, 28), nearExpiryThresholdDays: 3);

        Assert.Contains(alerts, alert => alert.AlertType == "LOW_STOCK" && alert.ProductId == lowStockProduct.ProductId);
        Assert.Contains(alerts, alert => alert.AlertType == "NEAR_EXPIRY" && alert.ProductId == nearExpiryProduct.ProductId && alert.BatchId.HasValue);
    }

    [Fact]
    public async Task Batch_history_reports_purchased_sold_wasted_remaining_and_status()
    {
        var harness = CreateHarness();
        var product = harness.Store.AddProduct("Chicken Breast", PricingType.WeightBased, "Kg", sellingPrice: 1_000m, reorderLevel: 10m);
        var batch = harness.Store.AddBatch(product.ProductId, remainingQuantity: 4m, costPrice: 700m, initialQuantity: 10m);
        harness.Store.SoldQuantities[batch.BatchId] = 3m;
        harness.Store.WastedQuantities[batch.BatchId] = 3m;

        var history = await harness.Service.GetBatchHistoryAsync(product.ProductId);

        Assert.Single(history);
        Assert.Equal(10m, history[0].PurchasedQuantity);
        Assert.Equal(3m, history[0].SoldQuantity);
        Assert.Equal(3m, history[0].WastedQuantity);
        Assert.Equal(4m, history[0].RemainingQuantity);
        Assert.Equal(BatchStatus.Active, history[0].Status);
    }

    private static TestHarness CreateHarness()
    {
        return new TestHarness();
    }

    private sealed class TestHarness
    {
        public Guid AdminId { get; } = Guid.NewGuid();
        public InMemoryInventoryDataStore Store { get; } = new();
        public RecordingAuditLogger AuditLogger { get; } = new();
        public InventoryService Service { get; }

        public TestHarness()
        {
            Service = new InventoryService(
                Store,
                new InlineTransactionRunner(),
                AuditLogger,
                new FixedClock(new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero)));
        }
    }

    private sealed class InMemoryInventoryDataStore : IInventoryDataStore
    {
        public List<ProductCategory> Categories { get; } = [];
        public List<Product> Products { get; } = [];
        public List<Batch> Batches { get; } = [];
        public List<WastageRecord> WastageRecords { get; } = [];
        public Dictionary<Guid, decimal> SoldQuantities { get; } = [];
        public Dictionary<Guid, decimal> WastedQuantities { get; } = [];

        public ProductCategory AddCategory(string name)
        {
            var category = new ProductCategory
            {
                Name = name,
                IsActive = true
            };
            Categories.Add(category);
            return category;
        }

        public Product AddProduct(
            string name,
            PricingType pricingType,
            string unit,
            decimal sellingPrice,
            decimal reorderLevel)
        {
            var category = Categories.FirstOrDefault() ?? AddCategory("Default");
            var product = new Product
            {
                CategoryId = category.CategoryId,
                Name = name,
                PricingType = pricingType,
                UnitOfMeasure = unit,
                SellingPrice = sellingPrice,
                ReorderLevel = reorderLevel,
                IsActive = true
            };
            Products.Add(product);
            return product;
        }

        public Batch AddBatch(
            Guid productId,
            decimal remainingQuantity,
            decimal costPrice = 500m,
            decimal? initialQuantity = null,
            DateTime? expiryDate = null,
            BatchStatus status = BatchStatus.Active)
        {
            var batch = new Batch
            {
                ProductId = productId,
                PurchaseDate = new DateTime(2026, 4, 28),
                ExpiryDate = expiryDate,
                InitialQuantity = initialQuantity ?? remainingQuantity,
                RemainingQuantity = remainingQuantity,
                CostPrice = costPrice,
                Status = status
            };
            Batches.Add(batch);
            return batch;
        }

        public Task<ProductCategory?> GetProductCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Categories.SingleOrDefault(category => category.CategoryId == categoryId));
        }

        public Task<ProductCategory?> GetProductCategoryByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Categories.SingleOrDefault(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddProductCategoryAsync(ProductCategory category, CancellationToken cancellationToken = default)
        {
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Products.SingleOrDefault(product => product.ProductId == productId));
        }

        public Task<Product?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Products.SingleOrDefault(product => string.Equals(product.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        public Task AddProductAsync(Product product, CancellationToken cancellationToken = default)
        {
            Products.Add(product);
            return Task.CompletedTask;
        }

        public Task AddBatchAsync(Batch batch, CancellationToken cancellationToken = default)
        {
            Batches.Add(batch);
            return Task.CompletedTask;
        }

        public Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Batches.SingleOrDefault(batch => batch.BatchId == batchId));
        }

        public Task<IReadOnlyList<Batch>> GetBatchesByProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Batch>>(Batches.Where(batch => batch.ProductId == productId).ToList());
        }

        public Task<IReadOnlyList<Batch>> GetAllBatchesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Batch>>(Batches.ToList());
        }

        public Task<IReadOnlyList<Product>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Product>>(Products.Where(product => product.IsActive).ToList());
        }

        public Task UpdateBatchAsync(Batch batch, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddWastageRecordAsync(WastageRecord wastageRecord, CancellationToken cancellationToken = default)
        {
            WastageRecords.Add(wastageRecord);
            WastedQuantities[wastageRecord.BatchId] = WastedQuantities.GetValueOrDefault(wastageRecord.BatchId) + wastageRecord.Quantity;
            return Task.CompletedTask;
        }

        public Task<decimal> GetSoldQuantityAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SoldQuantities.GetValueOrDefault(batchId));
        }

        public Task<decimal> GetWastedQuantityAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(WastedQuantities.GetValueOrDefault(batchId));
        }
    }

    private sealed class InlineTransactionRunner : ITransactionRunner
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset Now { get; } = now;
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
