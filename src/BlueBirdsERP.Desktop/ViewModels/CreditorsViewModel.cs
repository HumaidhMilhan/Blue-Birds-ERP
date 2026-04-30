using System.Collections.ObjectModel;
using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Desktop.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class CreditorsViewModel : ViewModelBase
{
    private readonly ICustomerAccountService _customerService;
    private readonly ILoginSessionFacade _loginFacade;

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

    partial void OnSelectedInvoiceChanged(DebtorAgingInvoice? value)
    {
        if (value is not null)
        {
            _ = LoadCustomerDetailsAsync(value.CustomerId);
        }
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

    private async Task LoadCustomerDetailsAsync(Guid customerId)
    {
        try
        {
            CreditSummary = await _customerService.GetCreditSummaryAsync(customerId);
            OnPropertyChanged(nameof(HasCreditSummary));

            var history = await _customerService.GetPaymentHistoryAsync(customerId);
            PaymentHistory.Clear();
            foreach (var entry in history)
                PaymentHistory.Add(entry);

            // Build a minimal CustomerAccountResult for display
            SelectedCustomer = new CustomerAccountResult(
                customerId,
                AccountId: null,
                Name: SelectedInvoice?.CustomerName ?? "Unknown",
                AccountType: Domain.Enums.AccountType.BusinessAccount,
                OutstandingBalance: CreditSummary.OutstandingBalance,
                CreditLimit: CreditSummary.CreditLimit,
                AvailableCredit: CreditSummary.AvailableCredit);
            OnPropertyChanged(nameof(HasSelectedCustomer));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load customer details: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateBusinessAccountAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        var dialog = new CreateBusinessAccountDialog(_loginFacade.CurrentUser.UserId);
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        IsBusy = true;
        try
        {
            await _customerService.CreateBusinessAccountAsync(dialog.Result);
            StatusMessage = "Business account created successfully.";
            await LoadDebtorsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create account: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateOneTimeCreditorAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        var dialog = new CreateOneTimeCreditorDialog(_loginFacade.CurrentUser.UserId);
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        IsBusy = true;
        try
        {
            await _customerService.CreateOneTimeCreditorAsync(dialog.Result);
            StatusMessage = "Walk-in creditor created successfully.";
            await LoadDebtorsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create creditor: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (SelectedCustomer is null || _loginFacade.CurrentUser is null) return;

        var outstanding = CreditSummary?.OutstandingBalance ?? 0m;
        var dialog = new RecordPaymentDialog(
            SelectedCustomer.CustomerId,
            _loginFacade.CurrentUser.UserId,
            outstanding);

        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        IsBusy = true;
        try
        {
            await _customerService.RecordAccountPaymentAsync(dialog.Result);
            StatusMessage = "Payment recorded successfully.";
            await LoadCustomerDetailsAsync(SelectedCustomer.CustomerId);
            await LoadDebtorsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to record payment: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditTermsAsync()
    {
        if (SelectedCustomer is null || _loginFacade.CurrentUser is null) return;

        var currentLimit = CreditSummary?.CreditLimit ?? 0m;
        var dialog = new EditTermsDialog(
            SelectedCustomer.CustomerId,
            _loginFacade.CurrentUser.UserId,
            currentLimit,
            30,   // default credit period (not available from CreditSummary)
            3);   // default notification lead

        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        IsBusy = true;
        try
        {
            await _customerService.UpdateBusinessAccountTermsAsync(dialog.Result);
            StatusMessage = "Credit terms updated successfully.";
            await LoadCustomerDetailsAsync(SelectedCustomer.CustomerId);
            await LoadDebtorsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update terms: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
