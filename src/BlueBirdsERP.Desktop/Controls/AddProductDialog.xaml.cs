using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Controls;

public partial class AddProductDialogWindow : Window
{
    public CreateProductRequest? Result { get; private set; }

    public AddProductDialogWindow()
    {
        InitializeComponent();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Product name is required.", "Validation");
            return;
        }
        if (string.IsNullOrWhiteSpace(UoMBox.Text))
        {
            MessageBox.Show("Unit of measure is required.", "Validation");
            return;
        }
        if (!decimal.TryParse(PriceBox.Text, out var price) || price <= 0)
        {
            MessageBox.Show("Enter a valid selling price.", "Validation");
            return;
        }
        if (!decimal.TryParse(ReorderBox.Text, out var reorder) || reorder < 0)
        {
            MessageBox.Show("Enter a valid reorder level.", "Validation");
            return;
        }

        var pricingType = PricingTypeBox.SelectedIndex == 0 ? PricingType.WeightBased : PricingType.UnitBased;

        Result = new CreateProductRequest(
            CategoryId: Guid.Empty, // TODO: category selection
            Name: NameBox.Text.Trim(),
            PricingType: pricingType,
            UnitOfMeasure: UoMBox.Text.Trim(),
            SellingPrice: price,
            ReorderLevel: reorder,
            CreatedBy: Guid.Empty, // TODO: from session
            Role: UserRole.Admin);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
