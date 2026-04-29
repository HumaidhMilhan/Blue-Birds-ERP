using System.Collections.ObjectModel;
using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly IReportingService _reportingService;
    private readonly ILoginSessionFacade _loginFacade;

    [ObservableProperty] private DateOnly _fromDate;
    [ObservableProperty] private DateOnly _toDate;
    [ObservableProperty] private OperationalReportResult? _report;

    // Sales
    public decimal TotalSales => Report?.Sales?.TotalSales ?? 0;
    public decimal RetailSales => Report?.Sales?.RetailSales ?? 0;
    public decimal WholesaleSales => Report?.Sales?.WholesaleSales ?? 0;
    public int InvoiceCount => Report?.Sales?.InvoiceCount ?? 0;

    // Profit
    public decimal GrossSales => Report?.Profit?.GrossSales ?? 0;
    public decimal CostOfGoodsSold => Report?.Profit?.CostOfGoodsSold ?? 0;
    public decimal GrossProfit => Report?.Profit?.GrossProfit ?? 0;
    public decimal ProfitMargin => GrossSales > 0 ? Math.Round(GrossProfit / GrossSales * 100, 1) : 0;

    // Payments
    public decimal CashPayments => Report?.Payments?.TotalCash ?? 0;
    public decimal CardPayments => Report?.Payments?.TotalCard ?? 0;
    public decimal TotalRefunds => Report?.Payments?.TotalRefunds ?? 0;
    public int PaymentCount => Report?.Payments?.PaymentCount ?? 0;

    // Wastage
    public decimal TotalWastage => Report?.Wastage?.TotalWastageValue ?? 0;
    public decimal CustomerReturnWastage => Report?.Wastage?.CustomerReturnWastageValue ?? 0;
    public int WastageCount => Report?.Wastage?.RecordCount ?? 0;

    // Sales Returns
    public decimal TotalReturnValue => Report?.SalesReturns?.TotalReturnValue ?? 0;
    public decimal TotalRefundAmount => Report?.SalesReturns?.TotalRefundAmount ?? 0;
    public int ReturnCount => Report?.SalesReturns?.ReturnCount ?? 0;

    // Collections
    public ObservableCollection<BatchMovementReportLine> BatchMovements { get; } = new();
    public ObservableCollection<StockOnHandReportLine> StockOnHand { get; } = new();

    public AnalyticsViewModel(IReportingService reportingService, ILoginSessionFacade loginFacade)
    {
        _reportingService = reportingService;
        _loginFacade = loginFacade;

        var today = DateOnly.FromDateTime(DateTime.Today);
        _fromDate = new DateOnly(today.Year, today.Month, 1);
        _toDate = today;
    }

    public override async Task LoadAsync()
    {
        await GenerateReportAsync();
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            Report = await _reportingService.GenerateOperationalReportAsync(new OperationalReportRequest(
                FromDate, ToDate,
                _loginFacade.CurrentUser.UserId,
                _loginFacade.CurrentUser.Role));

            // Populate batch movements
            BatchMovements.Clear();
            if (Report?.BatchMovements != null)
            {
                foreach (var item in Report.BatchMovements)
                    BatchMovements.Add(item);
            }

            // Populate stock on hand
            StockOnHand.Clear();
            if (Report?.StockOnHand != null)
            {
                foreach (var item in Report.StockOnHand)
                    StockOnHand.Add(item);
            }

            // Notify all computed property changes
            OnPropertyChanged(nameof(TotalSales));
            OnPropertyChanged(nameof(RetailSales));
            OnPropertyChanged(nameof(WholesaleSales));
            OnPropertyChanged(nameof(InvoiceCount));
            OnPropertyChanged(nameof(GrossSales));
            OnPropertyChanged(nameof(CostOfGoodsSold));
            OnPropertyChanged(nameof(GrossProfit));
            OnPropertyChanged(nameof(ProfitMargin));
            OnPropertyChanged(nameof(CashPayments));
            OnPropertyChanged(nameof(CardPayments));
            OnPropertyChanged(nameof(TotalRefunds));
            OnPropertyChanged(nameof(PaymentCount));
            OnPropertyChanged(nameof(TotalWastage));
            OnPropertyChanged(nameof(CustomerReturnWastage));
            OnPropertyChanged(nameof(WastageCount));
            OnPropertyChanged(nameof(TotalReturnValue));
            OnPropertyChanged(nameof(TotalRefundAmount));
            OnPropertyChanged(nameof(ReturnCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to generate report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
