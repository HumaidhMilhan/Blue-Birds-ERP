using System.Windows;

namespace BlueBirdsERP.Desktop.Controls;

public partial class PriceInputDialog : Window
{
    public decimal Price { get; private set; }

    public PriceInputDialog(string productName, string unitOfMeasure)
    {
        InitializeComponent();
        PromptText.Text = $"Enter selling price for {productName} (per {unitOfMeasure}):";
        PriceBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(PriceBox.Text, out var price) && price > 0)
        {
            Price = price;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please enter a valid price.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
