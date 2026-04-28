using BlueBirdsERP.Domain.Entities;

namespace BlueBirdsERP.Application.Inventory;

public interface IInventoryDataStore
{
    Task<ProductCategory?> GetProductCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<ProductCategory?> GetProductCategoryByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddProductCategoryAsync(ProductCategory category, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddProductAsync(Product product, CancellationToken cancellationToken = default);
    Task AddBatchAsync(Batch batch, CancellationToken cancellationToken = default);
    Task<Batch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> GetBatchesByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Batch>> GetAllBatchesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetActiveProductsAsync(CancellationToken cancellationToken = default);
    Task UpdateBatchAsync(Batch batch, CancellationToken cancellationToken = default);
    Task AddWastageRecordAsync(WastageRecord wastageRecord, CancellationToken cancellationToken = default);
    Task<decimal> GetSoldQuantityAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<decimal> GetWastedQuantityAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public sealed class InventoryValidationException(string message) : InvalidOperationException(message);
