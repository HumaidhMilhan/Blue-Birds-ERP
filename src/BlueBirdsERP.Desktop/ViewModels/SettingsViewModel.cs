using System.Collections.ObjectModel;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISystemSettingsService _settingsService;
    private readonly IUserManagementService _userManagementService;
    private readonly IDatabaseManagementService _dbManagementService;
    private readonly IAuditLogReader _auditLogReader;
    private readonly INotificationService _notificationService;
    private readonly ILoginSessionFacade _loginFacade;

    // Role flags
    [ObservableProperty] private bool _isAdmin;

    // Current user info
    [ObservableProperty] private string _profileUsername = string.Empty;
    [ObservableProperty] private string _profileRole = string.Empty;

    // System settings
    [ObservableProperty] private string _receiptCompanyName = string.Empty;
    [ObservableProperty] private string _receiptHeader = string.Empty;
    [ObservableProperty] private string _receiptFooter = string.Empty;

    // User management
    [ObservableProperty] private ObservableCollection<CashierAccountResult> _cashiers = new();
    [ObservableProperty] private string _newCashierUsername = string.Empty;

    // Database
    [ObservableProperty] private string _dbStatusMessage = string.Empty;
    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private OfflineSyncQueueStatusResult? _syncStatus;

    // Audit log
    [ObservableProperty] private DateOnly _auditFromDate;
    [ObservableProperty] private DateOnly _auditToDate;
    [ObservableProperty] private ObservableCollection<AuditLogEntryResult> _auditLogs = new();

    // Notification templates
    [ObservableProperty] private string _paymentReminderTemplate = string.Empty;
    [ObservableProperty] private string _overdueAlertTemplate = string.Empty;
    [ObservableProperty] private string _ownerDailyReportTemplate = string.Empty;

    public SettingsViewModel(
        ISystemSettingsService settingsService,
        IUserManagementService userManagementService,
        IDatabaseManagementService dbManagementService,
        IAuditLogReader auditLogReader,
        INotificationService notificationService,
        ILoginSessionFacade loginFacade)
    {
        _settingsService = settingsService;
        _userManagementService = userManagementService;
        _dbManagementService = dbManagementService;
        _auditLogReader = auditLogReader;
        _notificationService = notificationService;
        _loginFacade = loginFacade;

        var today = DateOnly.FromDateTime(DateTime.Today);
        _auditFromDate = today.AddDays(-30);
        _auditToDate = today;
    }

    public override async Task LoadAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsAdmin = _loginFacade.CurrentUser.Role == UserRole.Admin;
        ProfileUsername = _loginFacade.CurrentUser.Username;
        ProfileRole = _loginFacade.CurrentUser.Role.ToString();

        if (IsAdmin)
        {
            await LoadSystemSettingsAsync();
            await LoadSyncStatusAsync();
        }
    }

    [RelayCommand]
    private async Task LoadSystemSettingsAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        try
        {
            var settings = await _settingsService.GetSettingsAsync(new SystemSettingsQuery(
                _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));

            foreach (var s in settings)
            {
                switch (s.Key)
                {
                    case "Receipt.CompanyName": ReceiptCompanyName = s.Value; break;
                    case "Receipt.Header": ReceiptHeader = s.Value; break;
                    case "Receipt.Footer": ReceiptFooter = s.Value; break;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            await _settingsService.UpdateSettingsAsync(new UpdateSystemSettingsRequest(
                _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role,
                new List<SystemSettingUpdate>
                {
                    new("Receipt.CompanyName", ReceiptCompanyName, SystemSettingValueType.String),
                    new("Receipt.Header", ReceiptHeader, SystemSettingValueType.String),
                    new("Receipt.Footer", ReceiptFooter, SystemSettingValueType.String)
                }));

            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // User Management
    [RelayCommand]
    private async Task CreateCashierAsync()
    {
        if (_loginFacade.CurrentUser is null || string.IsNullOrWhiteSpace(NewCashierUsername)) return;

        IsBusy = true;
        try
        {
            var result = await _userManagementService.CreateCashierAsync(new CreateCashierRequest(
                NewCashierUsername, _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));

            Cashiers.Add(result);
            NewCashierUsername = string.Empty;
            StatusMessage = $"Cashier created. Temporary password: {result.TemporaryPassword}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create cashier: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateCashierAsync(CashierAccountResult? cashier)
    {
        if (cashier is null || _loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            await _userManagementService.DeactivateCashierAsync(new DeactivateCashierRequest(
                cashier.UserId, _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));

            Cashiers.Remove(cashier);
            StatusMessage = $"Cashier {cashier.Username} deactivated.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to deactivate cashier: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(CashierAccountResult? cashier)
    {
        if (cashier is null || _loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            var result = await _userManagementService.ResetCashierPasswordAsync(new ResetCashierPasswordRequest(
                cashier.UserId, _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));

            StatusMessage = $"Password reset for {cashier.Username}. New temporary password: {result.TemporaryPassword}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to reset password: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Database
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            var result = await _dbManagementService.TestSqliteConnectionAsync(new AdminOperationRequest(
                _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));

            DbStatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            DbStatusMessage = $"Connection test failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            var result = await _dbManagementService.BackupSqliteDatabaseAsync(new DatabaseBackupRequest(
                _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));

            BackupPath = result.BackupPath;
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Backup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSyncStatusAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        try
        {
            SyncStatus = await _dbManagementService.GetSyncQueueStatusAsync(new AdminOperationRequest(
                _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));
        }
        catch { /* silent */ }
    }

    // Audit Log
    [RelayCommand]
    private async Task SearchAuditLogsAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            var logs = await _auditLogReader.QueryAsync(new AuditLogQuery(
                _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role,
                AuditFromDate, AuditToDate));

            AuditLogs.Clear();
            foreach (var log in logs)
                AuditLogs.Add(log);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load audit logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Notification Templates
    [RelayCommand]
    private async Task SaveTemplatesAsync()
    {
        if (_loginFacade.CurrentUser is null) return;

        IsBusy = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(PaymentReminderTemplate))
            {
                await _notificationService.UpdateTemplateAsync(new UpdateNotificationTemplateRequest(
                    NotificationType.PaymentReminder, PaymentReminderTemplate,
                    _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));
            }
            if (!string.IsNullOrWhiteSpace(OverdueAlertTemplate))
            {
                await _notificationService.UpdateTemplateAsync(new UpdateNotificationTemplateRequest(
                    NotificationType.OverdueAlert, OverdueAlertTemplate,
                    _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));
            }
            if (!string.IsNullOrWhiteSpace(OwnerDailyReportTemplate))
            {
                await _notificationService.UpdateTemplateAsync(new UpdateNotificationTemplateRequest(
                    NotificationType.OwnerDailyReport, OwnerDailyReportTemplate,
                    _loginFacade.CurrentUser.UserId, _loginFacade.CurrentUser.Role));
            }
            StatusMessage = "Templates saved successfully.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save templates: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
