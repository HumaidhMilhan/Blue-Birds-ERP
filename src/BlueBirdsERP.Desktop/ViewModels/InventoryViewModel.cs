using System.Collections.ObjectModel;
using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILoginSessionFacade _loginFacade;

    [ObservableProperty] private ObservableCollection<ProductStockLevel> _products = new();
    [ObservableProperty] private ObservableCollection<ProductStockLevel> _filteredProducts = new();
    [ObservableProperty] private ObservableCollection<InventoryAlert> _alerts = new();
    [ObservableProperty] private ObservableCollection<BatchHistoryEntry> _batchHistory = new();
    [ObservableProperty] private ProductStockLevel? _selectedProduct;
    [ObservableProperty] private string _searchText = string.Empty;

    public bool HasAlerts => Alerts.Count > 0;
    public bool HasSelectedProduct => SelectedProduct is not null;

    public InventoryViewModel(IInventoryService inventoryService, ILoginSessionFacade loginFacade)
    {
        _inventoryService = inventoryService;
        _loginFacade = loginFacade;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedProductChanged(ProductStockLevel? value)
    {
        OnPropertyChanged(nameof(HasSelectedProduct));
        if (value is not null)
            _ = LoadBatchHistoryAsync();
    }

    public override async Task LoadAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            // Load products
            var products = await _inventoryService.GetProductStockLevelsAsync();
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);

            // Load alerts
            var alerts = await _inventoryService.GetInventoryAlertsAsync(DateOnly.FromDateTime(DateTime.Today), 7);
            Alerts.Clear();
            foreach (var a in alerts)
                Alerts.Add(a);
            OnPropertyChanged(nameof(HasAlerts));

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load inventory: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredProducts.Clear();
        var source = string.IsNullOrWhiteSpace(SearchText)
            ? Products
            : new ObservableCollection<ProductStockLevel>(
                Products.Where(p => p.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

        foreach (var p in source)
            FilteredProducts.Add(p);
    }

    [RelayCommand]
    private async Task LoadBatchHistoryAsync()
    {
        if (SelectedProduct is null) return;

        try
        {
            var history = await _inventoryService.GetBatchHistoryAsync(SelectedProduct.ProductId);
            BatchHistory.Clear();
            foreach (var h in history)
                BatchHistory.Add(h);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load batch history: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddProductAsync()
    {
        // TODO: Open AddProductDialog with proper VM, then call:
        // await _inventoryService.CreateProductAsync(request);
        // await LoadDataAsync();
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        // TODO: Open AddCategoryDialog, then call:
        // await _inventoryService.CreateProductCategoryAsync(request);
    }

    [RelayCommand]
    private async Task RecordPurchaseAsync()
    {
        // TODO: Open RecordPurchaseDialog, then call:
        // await _inventoryService.RecordManualBatchPurchaseAsync(request);
        // await LoadDataAsync();
    }

    [RelayCommand]
    private async Task RecordWastageAsync()
    {
        // TODO: Open RecordWastageDialog, then call:
        // await _inventoryService.RecordWastageAsync(request);
        // await LoadDataAsync();
    }
}
