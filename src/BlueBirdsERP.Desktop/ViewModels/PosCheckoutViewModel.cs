using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Desktop.Controls;
using BlueBirdsERP.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class PosCheckoutViewModel : ViewModelBase
{
    private readonly IPOSCheckoutService _checkoutService;
    private readonly IInventoryService _inventoryService;
    private readonly ICustomerAccountService _customerService;
    private readonly IReceiptPrinter _receiptPrinter;
    private readonly ILoginSessionFacade _loginFacade;

    // Sale channel
    [ObservableProperty] private SaleChannel _selectedSaleChannel = SaleChannel.Retail;
    [ObservableProperty] private IReadOnlyList<SaleChannel> _saleChannels = [];

    // Customer
    [ObservableProperty] private string _customerSearchText = string.Empty;
    [ObservableProperty] private CustomerSearchResult? _selectedCustomer;
    [ObservableProperty] private CreditSummary? _creditSummary;
    [ObservableProperty] private bool _hasCustomer;

    // Line items
    [ObservableProperty] private ObservableCollection<LineItemViewModel> _lineItems = new();

    // Payment
    [ObservableProperty] private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private IReadOnlyList<PaymentMethod> _allowedPaymentMethods = [];
    [ObservableProperty] private decimal _cashAmount;
    [ObservableProperty] private decimal _cardAmount;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private bool _isCreditPayment;

    // UI state
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private LineItemViewModel? _selectedLineItem;

    // Receipt
    [ObservableProperty] private InvoiceReceipt? _lastReceipt;
    [ObservableProperty] private bool _hasReceipt;
    [ObservableProperty] private string _lastInvoiceNumber = string.Empty;

    // Computed totals
    public decimal Subtotal => LineItems.Sum(l => l.LineTotal);
    public decimal DiscountTotal => LineItems.Sum(l => l.DiscountAmount);
    public decimal GrandTotal => Subtotal - DiscountTotal;
    public decimal PaidAmount => SelectedPaymentMethod == PaymentMethod.Mixed
        ? CashAmount + CardAmount
        : (SelectedPaymentMethod == PaymentMethod.Credit ? 0 : GrandTotal);
    public decimal BalanceAmount => GrandTotal - PaidAmount;

    public PosCheckoutViewModel(
        IPOSCheckoutService checkoutService,
        IInventoryService inventoryService,
        ICustomerAccountService customerService,
        IReceiptPrinter receiptPrinter,
        ILoginSessionFacade loginFacade)
    {
        _checkoutService = checkoutService;
        _inventoryService = inventoryService;
        _customerService = customerService;
        _receiptPrinter = receiptPrinter;
        _loginFacade = loginFacade;

        SaleChannels = _checkoutService.GetSaleChannels();
        UpdateAllowedPaymentMethods();

        LineItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(DiscountTotal));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(PaidAmount));
            OnPropertyChanged(nameof(BalanceAmount));
        };
    }

    partial void OnSelectedSaleChannelChanged(SaleChannel value)
    {
        UpdateAllowedPaymentMethods();
    }

    partial void OnSelectedPaymentMethodChanged(PaymentMethod value)
    {
        IsCreditPayment = value is PaymentMethod.Credit or PaymentMethod.Mixed;
        OnPropertyChanged(nameof(PaidAmount));
        OnPropertyChanged(nameof(BalanceAmount));
    }

    partial void OnCashAmountChanged(decimal value) => OnPropertyChanged(nameof(PaidAmount));
    partial void OnCardAmountChanged(decimal value) => OnPropertyChanged(nameof(PaidAmount));

    private void UpdateAllowedPaymentMethods()
    {
        AllowedPaymentMethods = _checkoutService.GetAllowedPaymentMethods(SelectedSaleChannel);
        if (!AllowedPaymentMethods.Contains(SelectedPaymentMethod))
            SelectedPaymentMethod = AllowedPaymentMethods.First();
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        try
        {
            var catalog = await _inventoryService.GetProductCatalogAsync();
            if (catalog.Count == 0)
            {
                StatusMessage = "No products available. Add products in Inventory first.";
                return;
            }

            // Show product selection dialog (with prices)
            var productDialog = new ProductPickerDialog(catalog);
            if (productDialog.ShowDialog() != true || productDialog.SelectedProduct == null)
                return;

            var selectedProduct = productDialog.SelectedProduct;

            // Show batch picker
            var batchOptions = await _checkoutService.GetBatchPickerOptionsAsync(selectedProduct.ProductId);
            if (batchOptions.Count == 0)
            {
                StatusMessage = $"No available batches for {selectedProduct.Name}.";
                return;
            }

            var batchDialog = new BatchPickerDialog(batchOptions);
            if (batchDialog.ShowDialog() != true || batchDialog.SelectedBatch == null)
                return;

            var unitPrice = selectedProduct.SellingPrice;

            var lineItem = new LineItemViewModel
            {
                ProductId = selectedProduct.ProductId,
                BatchId = batchDialog.SelectedBatch.BatchId,
                ProductName = selectedProduct.Name,
                BatchReference = batchDialog.SelectedBatch.BatchReference,
                UnitOfMeasure = selectedProduct.UnitOfMeasure,
                Quantity = batchDialog.Quantity,
                UnitPrice = unitPrice,
                DiscountAmount = 0
            };

            lineItem.PropertyChanged += LineItemPropertyChanged;
            LineItems.Add(lineItem);

            RecalculateTotals();
            StatusMessage = $"Added {lineItem.ProductName} x{lineItem.Quantity}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding item: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveItem(LineItemViewModel? item)
    {
        if (item == null) return;
        item.PropertyChanged -= LineItemPropertyChanged;
        LineItems.Remove(item);
        RecalculateTotals();
    }

    [RelayCommand]
    private async Task SearchCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchText)) return;

        try
        {
            StatusMessage = "Searching customers...";
            var results = await _customerService.SearchCustomersAsync(CustomerSearchText);

            if (results.Count == 0)
            {
                StatusMessage = "No customers found.";
                return;
            }

            if (results.Count == 1)
            {
                var match = results[0];
                SelectedCustomer = match;
                HasCustomer = true;
                CustomerSearchText = match.Name;

                // Load credit summary
                CreditSummary = await _customerService.GetCreditSummaryAsync(match.CustomerId);
                StatusMessage = $"Customer: {match.Name}";
            }
            else
            {
                // Multiple matches — pick the first one and show count
                var match = results[0];
                SelectedCustomer = match;
                HasCustomer = true;
                CustomerSearchText = match.Name;

                CreditSummary = await _customerService.GetCreditSummaryAsync(match.CustomerId);
                StatusMessage = $"Found {results.Count} customers. Showing: {match.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        SelectedCustomer = null;
        CreditSummary = null;
        HasCustomer = false;
        CustomerSearchText = string.Empty;
    }

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        if (LineItems.Count == 0)
        {
            StatusMessage = "Add at least one item before completing the sale.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Processing sale...";

        try
        {
            var request = new CheckoutRequest(
                SaleChannel: SelectedSaleChannel,
                CustomerId: SelectedCustomer?.CustomerId,
                CashierId: _loginFacade.CurrentUser?.UserId ?? Guid.Empty,
                PaymentMethod: SelectedPaymentMethod,
                Lines: LineItems.Select(l => new CheckoutLineItem(
                    l.ProductId, l.BatchId, l.Quantity, l.UnitPrice, l.DiscountAmount)).ToList(),
                CashAmount: CashAmount,
                CardAmount: CardAmount,
                CreditAmount: SelectedPaymentMethod == PaymentMethod.Credit ? GrandTotal : 0,
                ManualDueDate: DueDate,
                Notes: string.IsNullOrWhiteSpace(Notes) ? null : Notes);

            var result = await _checkoutService.CheckoutAsync(request);

            LastReceipt = result.Receipt;
            LastInvoiceNumber = result.InvoiceNumber;
            HasReceipt = true;
            StatusMessage = $"Sale complete! Invoice: {result.InvoiceNumber} | Total: Rs. {result.GrandTotal:N2}";

            // Try to print receipt
            try
            {
                await _receiptPrinter.PrintInvoiceAsync(result.InvoiceId);
            }
            catch
            {
                // Printing is best-effort
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sale failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewSale()
    {
        LineItems.Clear();
        SelectedCustomer = null;
        CreditSummary = null;
        HasCustomer = false;
        CustomerSearchText = string.Empty;
        CashAmount = 0;
        CardAmount = 0;
        DueDate = null;
        Notes = string.Empty;
        LastReceipt = null;
        HasReceipt = false;
        LastInvoiceNumber = string.Empty;
        StatusMessage = string.Empty;
        SelectedSaleChannel = SaleChannel.Retail;
    }

    private void LineItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LineItemViewModel.Quantity) ||
            e.PropertyName == nameof(LineItemViewModel.DiscountAmount))
        {
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DiscountTotal));
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(PaidAmount));
        OnPropertyChanged(nameof(BalanceAmount));
    }
}

public partial class LineItemViewModel : ObservableObject
{
    [ObservableProperty] private Guid _productId;
    [ObservableProperty] private Guid _batchId;
    [ObservableProperty] private string _productName = string.Empty;
    [ObservableProperty] private string _batchReference = string.Empty;
    [ObservableProperty] private string _unitOfMeasure = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _discountAmount;

    public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;
    public string DisplayQuantity => $"{Quantity} {UnitOfMeasure}";

    partial void OnQuantityChanged(decimal value)
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(DisplayQuantity));
    }

    partial void OnDiscountAmountChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
}
