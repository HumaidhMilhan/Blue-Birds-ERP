using System.Collections.ObjectModel;
using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlueBirdsERP.Desktop.Controls;

public partial class ProductPickerDialog : Window
{
    public ProductStockLevel? SelectedProduct { get; private set; }

    public ProductPickerDialog(IReadOnlyList<ProductStockLevel> products)
    {
        InitializeComponent();
        DataContext = new ProductPickerViewModel(products);
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProductPickerViewModel vm && vm.SelectedProduct != null)
        {
            SelectedProduct = vm.SelectedProduct;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please select a product.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public partial class ProductPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ProductStockLevel> _allProducts;

    public ObservableCollection<ProductStockLevel> FilteredProducts { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ProductStockLevel? _selectedProduct;

    public ProductPickerViewModel(IReadOnlyList<ProductStockLevel> products)
    {
        _allProducts = products;
        foreach (var p in products)
            FilteredProducts.Add(p);
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredProducts.Clear();
        var filtered = string.IsNullOrWhiteSpace(value)
            ? _allProducts
            : _allProducts.Where(p => p.ProductName.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var p in filtered)
            FilteredProducts.Add(p);
    }
}
