using System.Collections.ObjectModel;
using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class CreditorsViewModel : ViewModelBase
{
    private readonly ICustomerAccountService _customerService;
    private readonly ILoginSessionFacade _loginFacade;

    // TODO: Backend needs ICustomerAccountService.GetAllCustomersAsync()
    // Currently using DebtorAgingReport as a workaround to list customers

    [ObservableProperty] private ObservableCollection<DebtorAgingBucket> _debtorBuckets = new();
    [ObservableProperty] private DebtorAgingInvoice? _selectedInvoice;
    [ObservableProperty] private CustomerAccountResult? _selectedCustomer;
    [ObservableProperty] private CreditSummary? _creditSummary;
    [ObservableProperty] private ObservableCollection<CustomerPaymentHistoryEntry> _paymentHistory = new();
    [ObservableProperty] private string _searchText = string.Empty;

    public bool HasSelectedCustomer => SelectedCustomer is not null;
    public bool HasCreditSummary => CreditSummary is not null;

    public CreditorsViewModel(ICustomerAccountService customerService, ILoginSessionFacade loginFacade)
    {
        _customerService = customerService;
        _loginFacade = loginFacade;
    }

    public override async Task LoadAsync()
    {
        await LoadDebtorsAsync();
    }

    [RelayCommand]
    private async Task LoadDebtorsAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var report = await _customerService.GenerateDebtorAgingReportAsync(DateOnly.FromDateTime(DateTime.Today));
            DebtorBuckets.Clear();
            if (report?.Buckets != null)
            {
                foreach (var bucket in report.Buckets)
                    DebtorBuckets.Add(bucket);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load creditors: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadCustomerDetailsAsync()
    {
        // TODO: When a customer row is selected, load their credit summary and payment history
        // This requires a customer ID which we don't have from DebtorAgingInvoice alone
        // Backend needs: ICustomerAccountService.GetAllCustomersAsync()
    }

    [RelayCommand]
    private async Task CreateBusinessAccountAsync()
    {
        // TODO: Open CreateBusinessAccountDialog, then call:
        // await _customerService.CreateBusinessAccountAsync(request);
        // await LoadDebtorsAsync();
    }

    [RelayCommand]
    private async Task CreateOneTimeCreditorAsync()
    {
        // TODO: Open CreateCreditorDialog, then call:
        // await _customerService.CreateOneTimeCreditorAsync(request);
        // await LoadDebtorsAsync();
    }

    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (SelectedCustomer is null) return;

        // TODO: Open RecordPaymentDialog, then call:
        // await _customerService.RecordAccountPaymentAsync(request);
        // await LoadCustomerDetailsAsync();
    }

    [RelayCommand]
    private async Task EditTermsAsync()
    {
        if (SelectedCustomer is null) return;

        // TODO: Open EditTermsDialog, then call:
        // await _customerService.UpdateBusinessAccountTermsAsync(request);
        // await LoadCustomerDetailsAsync();
    }
}
