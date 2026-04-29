using System.Collections.ObjectModel;
using System.Windows.Threading;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Desktop.Services;
using BlueBirdsERP.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly ILoginSessionFacade _loginFacade;
    private readonly ISessionService _sessionService;
    private readonly IRbacAuthorizationService _rbacService;
    private readonly DispatcherTimer _sessionTimer;
    private readonly DispatcherTimer _clockTimer;
    private DateTime _lastActivity;
    private AuthenticatedUser? _currentUser;

    [ObservableProperty] private ObservableObject? _currentView;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private string _currentUsername = string.Empty;
    [ObservableProperty] private string _currentRole = string.Empty;
    [ObservableProperty] private string _currentPageTitle = string.Empty;
    [ObservableProperty] private string _currentDateTime = string.Empty;
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private ObservableCollection<NavigationItem> _navigationItems = new();
    [ObservableProperty] private NavigationItem? _selectedNavigationItem;

    // Page keys for navigation
    public const string PagePos = "POS";
    public const string PageDashboard = "Dashboard";
    public const string PageCreditors = "Creditors";
    public const string PageAnalytics = "Analytics";
    public const string PageInventory = "Inventory";
    public const string PageSettings = "Settings";

    public MainViewModel(
        IServiceProvider serviceProvider,
        INavigationService navigationService,
        ILoginSessionFacade loginFacade,
        ISessionService sessionService,
        IRbacAuthorizationService rbacService)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        _loginFacade = loginFacade;
        _sessionService = sessionService;
        _rbacService = rbacService;

        _navigationService.NavigationChanged += () =>
        {
            CurrentView = _navigationService.CurrentView;
        };

        // Session timeout timer (1-min tick, 15-min timeout)
        _sessionTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _sessionTimer.Tick += SessionTimerTick;

        // Clock timer (1-sec tick)
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) => CurrentDateTime = DateTime.Now.ToString("ddd, MMM dd yyyy  •  HH:mm:ss");
        _clockTimer.Start();
        CurrentDateTime = DateTime.Now.ToString("ddd, MMM dd yyyy  •  HH:mm:ss");

        ShowLogin();
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value?.NavigateCommand?.CanExecute(null) == true)
        {
            value.NavigateCommand.Execute(null);
        }
    }

    private void ShowLogin()
    {
        IsLoggedIn = false;
        CurrentUsername = string.Empty;
        CurrentRole = string.Empty;
        CurrentPageTitle = string.Empty;
        _currentUser = null;
        NavigationItems.Clear();
        _sessionTimer.Stop();

        var loginVm = _serviceProvider.GetRequiredService<LoginViewModel>();
        loginVm.LoginSucceeded = OnLoginSucceeded;
        _navigationService.NavigateTo(loginVm);
    }

    private async void OnLoginSucceeded(LoginResult result)
    {
        IsLoggedIn = true;
        _currentUser = result.User;
        CurrentUsername = result.User!.Username;
        CurrentRole = result.User.Role.ToString();

        await _sessionService.BeginSessionAsync(result.User);
        _lastActivity = DateTime.UtcNow;
        _sessionTimer.Start();

        BuildNavigationItems(result.Permissions);

        // Navigate to first available page
        if (NavigationItems.Count > 0)
        {
            SelectedNavigationItem = NavigationItems[0];
        }
    }

    private void BuildNavigationItems(IReadOnlySet<RbacPermission> permissions)
    {
        NavigationItems.Clear();

        // POS — Cashier and Admin
        if (permissions.Contains(RbacPermission.PosBilling))
        {
            NavigationItems.Add(new NavigationItem(
                "POS Billing",
                "M10,2H3V22H10V14H14V22H21V2H14V10H10V2Z",
                PagePos,
                RbacPermission.PosBilling,
                NavigateToPos));
        }

        // Dashboard — Admin only (requires Reporting)
        if (permissions.Contains(RbacPermission.Reporting))
        {
            NavigationItems.Add(new NavigationItem(
                "Dashboard",
                "M3,3V21H21V19H5V3H3M14,7A2,2 0 0,1 16,9A2,2 0 0,1 14,11A2,2 0 0,1 12,9A2,2 0 0,1 14,7M14,13C17.31,13 20,14.79 20,17V19H8V17C8,14.79 10.69,13 14,13M14,15C11.79,15 10,15.9 10,17V18H18V17C18,15.9 16.21,15 14,15Z",
                PageDashboard,
                RbacPermission.Reporting,
                NavigateToDashboard));
        }

        // Creditors — Cashier (read) and Admin
        if (permissions.Contains(RbacPermission.CustomerAccountManagement) ||
            permissions.Contains(RbacPermission.CustomerReadOnlyLookup))
        {
            NavigationItems.Add(new NavigationItem(
                "Creditors",
                "M16,13C14.69,13 13.38,13.28 12.27,13.85C11.34,13.34 10.25,13.07 9.07,13.07C6.13,13.07 3.5,14.95 2.28,17.71L1,16.07C2.5,12.73 5.5,10.5 9.07,10.5C10.55,10.5 11.92,10.87 13.09,11.5C13.84,10.63 14.87,10.06 16,10.06C18.34,10.06 20.24,11.96 20.24,14.3C20.24,14.53 20.22,14.76 20.18,14.97C20.78,14.37 21.5,13.89 22.31,13.59L23,16.07C21.82,16.55 20.82,17.32 20.1,18.28C21.14,18.78 22,19.59 22.57,20.59L21.15,21.5C20.22,19.87 18.36,18.78 16.24,18.78C14.9,18.78 13.67,19.13 12.6,19.72L11.4,17.69C12.58,17.19 13.9,16.89 15.3,16.89C17.64,16.89 19.65,18.24 20.64,20.19C19.85,20.72 18.83,21 17.7,21C15.2,21 13.13,19.28 12.5,17H10.5C10.5,17 10.5,17 10.5,17C11.13,19.28 9.06,21 6.56,21C4.06,21 2,19.28 2,17C2,14.72 4.06,13 6.56,13H16Z",
                PageCreditors,
                RbacPermission.CustomerAccountManagement,
                NavigateToCreditors));
        }

        // Analytics — Admin only
        if (permissions.Contains(RbacPermission.Reporting))
        {
            NavigationItems.Add(new NavigationItem(
                "Analytics",
                "M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z",
                PageAnalytics,
                RbacPermission.Reporting,
                NavigateToAnalytics));
        }

        // Inventory — Cashier (read) and Admin
        if (permissions.Contains(RbacPermission.InventoryManagement) ||
            permissions.Contains(RbacPermission.BatchManagement))
        {
            NavigationItems.Add(new NavigationItem(
                "Inventory",
                "M19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5A2,2 0 0,0 19,3M19,19H5V5H19V19M17,17H7V7H17V17Z",
                PageInventory,
                RbacPermission.InventoryManagement,
                NavigateToInventory));
        }

        // Settings — Admin only
        if (permissions.Contains(RbacPermission.SystemConfiguration))
        {
            NavigationItems.Add(new NavigationItem(
                "Settings",
                "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.04 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.04 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z",
                PageSettings,
                RbacPermission.SystemConfiguration,
                NavigateToSettings));
        }
    }

    [RelayCommand]
    private void NavigateToPos()
    {
        var vm = _serviceProvider.GetRequiredService<PosCheckoutViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "POS Billing";
        Touch();
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Dashboard";
        Touch();
    }

    [RelayCommand]
    private void NavigateToCreditors()
    {
        var vm = _serviceProvider.GetRequiredService<CreditorsViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Creditors";
        Touch();
    }

    [RelayCommand]
    private void NavigateToAnalytics()
    {
        var vm = _serviceProvider.GetRequiredService<AnalyticsViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Analytics";
        Touch();
    }

    [RelayCommand]
    private void NavigateToInventory()
    {
        var vm = _serviceProvider.GetRequiredService<InventoryViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Inventory";
        Touch();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        var vm = _serviceProvider.GetRequiredService<SettingsViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Settings";
        Touch();
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _sessionTimer.Stop();
        await _loginFacade.LogoutAsync();
        ShowLogin();
    }

    public void Touch()
    {
        _lastActivity = DateTime.UtcNow;
        _sessionService.TouchAsync();
    }

    private async void SessionTimerTick(object? sender, EventArgs e)
    {
        if (!IsLoggedIn) return;

        var inactiveTime = DateTime.UtcNow - _lastActivity;
        if (inactiveTime >= _sessionService.InactivityTimeout)
        {
            _sessionTimer.Stop();
            await _sessionService.EndSessionAsync();
            ShowLogin();
        }
    }
}
