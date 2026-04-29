using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlueBirdsERP.Desktop.Controls;

public partial class BatchPickerDialog : Window
{
    public BatchPickerOption? SelectedBatch { get; private set; }
    public decimal Quantity { get; private set; }

    public BatchPickerDialog(IReadOnlyList<BatchPickerOption> batches)
    {
        InitializeComponent();
        DataContext = new BatchPickerViewModel(batches);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BatchPickerViewModel vm)
        {
            if (vm.SelectedBatch == null)
            {
                MessageBox.Show("Please select a batch.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (vm.Quantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (vm.Quantity > vm.SelectedBatch.RemainingQuantity)
            {
                MessageBox.Show($"Quantity exceeds available stock ({vm.SelectedBatch.RemainingQuantity:N2}).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedBatch = vm.SelectedBatch;
            Quantity = vm.Quantity;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public partial class BatchPickerViewModel : ObservableObject
{
    public IReadOnlyList<BatchPickerOption> Batches { get; }
    [ObservableProperty] private BatchPickerOption? _selectedBatch;
    [ObservableProperty] private decimal _quantity = 1;

    public BatchPickerViewModel(IReadOnlyList<BatchPickerOption> batches)
    {
        Batches = batches;
        if (batches.Count > 0)
            SelectedBatch = batches[0];
    }
}
