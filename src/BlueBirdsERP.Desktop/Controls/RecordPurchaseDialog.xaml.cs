using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Controls;

public partial class RecordPurchaseDialogWindow : Window
{
    public ManualBatchPurchaseRequest? Result { get; private set; }

    public RecordPurchaseDialogWindow(IReadOnlyList<ProductStockLevel> products)
    {
        InitializeComponent();
        ProductBox.ItemsSource = products;
        if (products.Count > 0)
            ProductBox.SelectedIndex = 0;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (ProductBox.SelectedItem is not ProductStockLevel product)
        {
            MessageBox.Show("Select a product.", "Validation");
            return;
        }
        if (!decimal.TryParse(QuantityBox.Text, out var qty) || qty <= 0)
        {
            MessageBox.Show("Enter a valid quantity.", "Validation");
            return;
        }
        if (!decimal.TryParse(CostPriceBox.Text, out var cost) || cost <= 0)
        {
            MessageBox.Show("Enter a valid cost price.", "Validation");
            return;
        }

        Result = new ManualBatchPurchaseRequest(
            ProductId: product.ProductId,
            PurchaseDate: PurchaseDatePicker.SelectedDate ?? DateTime.Today,
            ExpiryDate: ExpiryDatePicker.SelectedDate,
            InitialQuantity: qty,
            CostPrice: cost,
            RecordedBy: Guid.Empty, // TODO: from session
            Role: UserRole.Admin);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
