using System.Collections.ObjectModel;
using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IReportingService _reportingService;
    private readonly ILoginSessionFacade _loginFacade;

    [ObservableProperty] private DateOnly _fromDate;
    [ObservableProperty] private DateOnly _toDate;
    [ObservableProperty] private OperationalReportResult? _report;

    // KPI computed properties
    public decimal TotalSales => Report?.Sales?.TotalSales ?? 0;
    public decimal GrossProfit => Report?.Profit?.GrossProfit ?? 0;
    public decimal WastageValue => Report?.Wastage?.TotalWastageValue ?? 0;
    public int InvoiceCount => Report?.Sales?.InvoiceCount ?? 0;
    public decimal RetailSales => Report?.Sales?.RetailSales ?? 0;
    public decimal WholesaleSales => Report?.Sales?.WholesaleSales ?? 0;
    public decimal CashPayments => Report?.Payments?.TotalCash ?? 0;
    public decimal CardPayments => Report?.Payments?.TotalCard ?? 0;
    public decimal TotalRefunds => Report?.Payments?.TotalRefunds ?? 0;

    public ObservableCollection<StockOnHandReportLine> LowStockItems { get; } = new();
    public ObservableCollection<DebtorAgingBucket> DebtorBuckets { get; } = new();

    public DashboardViewModel(IReportingService reportingService, ILoginSessionFacade loginFacade)
    {
        _reportingService = reportingService;
        _loginFacade = loginFacade;

        var today = DateOnly.FromDateTime(DateTime.Today);
        _fromDate = new DateOnly(today.Year, today.Month, 1);
        _toDate = today;
    }

    public override async Task LoadAsync()
    {
        await LoadReportAsync();
    }

    [RelayCommand]
    private async Task LoadReportAsync()
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

            // Populate low stock items
            LowStockItems.Clear();
            if (Report?.StockOnHand != null)
            {
                foreach (var item in Report.StockOnHand.Where(s => s.RemainingQuantity < s.ReorderLevel))
                    LowStockItems.Add(item);
            }

            // Populate debtor buckets
            DebtorBuckets.Clear();
            if (Report?.DebtorAging?.Buckets != null)
            {
                foreach (var bucket in Report.DebtorAging.Buckets)
                    DebtorBuckets.Add(bucket);
            }

            // Notify KPI changes
            OnPropertyChanged(nameof(TotalSales));
            OnPropertyChanged(nameof(GrossProfit));
            OnPropertyChanged(nameof(WastageValue));
            OnPropertyChanged(nameof(InvoiceCount));
            OnPropertyChanged(nameof(RetailSales));
            OnPropertyChanged(nameof(WholesaleSales));
            OnPropertyChanged(nameof(CashPayments));
            OnPropertyChanged(nameof(CardPayments));
            OnPropertyChanged(nameof(TotalRefunds));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
