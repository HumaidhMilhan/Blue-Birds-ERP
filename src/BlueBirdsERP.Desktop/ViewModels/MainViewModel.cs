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
    private readonly DispatcherTimer _clockTimer;
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
                "M 17 6V4H7V6H17M 19 16V8H5V16H19M 19 18H5C3.9 18 3 17.1 3 16V8C3 6.9 3.9 6 5 6V4C5 2.9 5.9 2 7 2H17C18.1 2 19 2.9 19 4V6C20.1 6 21 6.9 21 8V16C21 17.1 20.1 18 19 18M 11 11H7V13H11V11M 17 11H13V13H17V11Z",
                PagePos,
                RbacPermission.PosBilling,
                NavigateToPos));
        }

        // Dashboard — Admin only (requires Reporting)
        if (permissions.Contains(RbacPermission.Reporting))
        {
            NavigationItems.Add(new NavigationItem(
                "Dashboard",
                "M4,4H10V12H4V4M4,14H10V20H4V14M14,4H20V10H14V4M14,12H20V20H14V12M6,6V10H8V6H6M6,16V18H8V16H6M16,6V8H18V6H16M16,14V18H18V14H16Z",
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
                "M16,11C17.66,11 18.99,9.66 18.99,8C18.99,6.34 17.66,5 16,5C14.34,5 13,6.34 13,8C13,9.66 14.34,11 16,11M16,7C16.55,7 17,7.45 17,8C17,8.55 16.55,9 16,9C15.45,9 15,8.55 15,8C15,7.45 15.45,7 16,7M8,11C9.66,11 10.99,9.66 10.99,8C10.99,6.34 9.66,5 8,5C6.34,5 5,6.34 5,8C5,9.66 6.34,11 8,11M8,7C8.55,7 9,7.45 9,8C9,8.55 8.55,9 8,9C7.45,9 7,8.55 7,8C7,7.45 7.45,7 8,7M16,13C13.67,13 9,14.17 9,16.5V19H23V16.5C23,14.17 18.33,13 16,13M11.24,17H20.76V16.5C20.76,15.91 18.23,15 16,15C13.77,15 11.24,15.91 11.24,16.5V17M8,13C7.53,13 7.03,13.06 6.55,13.17C5.02,13.56 3,14.54 3,16.5V19H7V17H5.24V16.5C5.24,15.54 6.78,14.56 8.35,14.17C8.25,13.8 8.16,13.4 8,13Z",
                PageCreditors,
                RbacPermission.CustomerAccountManagement,
                NavigateToCreditors));
        }

        // Analytics — Admin only
        if (permissions.Contains(RbacPermission.Reporting))
        {
            NavigationItems.Add(new NavigationItem(
                "Analytics",
                "M 16,11.78L 20.24,4.45L 21.97,5.45L 16.74,14.5L 10.23,10.75L 5.46,19H 22V 21H 2V 3H 4V 17.54L 9.5,8L 16,11.78Z",
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
                "M21,16.5C21,16.88 20.79,17.21 20.47,17.38L12.57,21.82C12.41,21.94 12.21,22 12,22C11.79,22 11.59,21.94 11.43,21.82L3.53,17.38C3.21,17.21 3,16.88 3,16.5V7.5C3,7.12 3.21,6.79 3.53,6.62L11.43,2.18C11.59,2.06 11.79,2 12,2C12.21,2 12.41,2.06 12.57,2.18L20.47,6.62C20.79,6.79 21,7.12 21,7.5V16.5M12,4.15L5,8.09L12,12.03L19,8.09L12,4.15M5,15.91L11,19.85V13.18L5,9.24V15.91M19,15.91V9.24L13,13.18V19.85L19,15.91Z",
                PageInventory,
                RbacPermission.InventoryManagement,
                NavigateToInventory));
        }

        // Settings — Admin only
        if (permissions.Contains(RbacPermission.SystemConfiguration))
        {
            NavigationItems.Add(new NavigationItem(
                "Settings",
                "M19.14,12.94C19.3,12.61 19.3,12.3 19.3,12C19.3,11.7 19.3,11.39 19.14,11.06L21.25,9.41C21.44,9.26 21.5,8.97 21.36,8.74L19.36,5.28C19.24,5.04 18.96,4.95 18.72,5.04L16.23,6.04C15.71,5.65 15.17,5.31 14.54,5.06L14.17,2.41C14.13,2.17 13.92,2 13.67,2H9.67C9.42,2 9.21,2.17 9.17,2.41L8.8,5.06C8.17,5.31 7.63,5.65 7.11,6.04L4.62,5.04C4.38,4.95 4.1,5.04 3.98,5.28L1.98,8.74C1.84,8.97 1.9,9.26 2.09,9.41L4.2,11.06C4.04,11.39 4.04,11.7 4.04,12C4.04,12.3 4.04,12.61 4.2,12.94L2.09,14.59C1.9,14.74 1.84,15.03 1.98,15.26L3.98,18.72C4.1,18.96 4.38,19.05 4.62,18.96L7.11,17.96C7.63,18.35 8.17,18.69 8.8,18.94L9.17,21.59C9.21,21.83 9.42,22 9.67,22H13.67C13.92,22 14.13,21.83 14.17,21.59L14.54,18.94C15.17,18.69 15.71,18.35 16.23,17.96L18.72,18.96C18.96,19.05 19.24,18.96 19.36,18.72L21.36,15.26C21.5,15.03 21.44,14.74 21.25,14.59L19.14,12.94M12,15.5C10.07,15.5 8.5,13.93 8.5,12C8.5,10.07 10.07,8.5 12,8.5C13.93,8.5 15.5,10.07 15.5,12C15.5,13.93 13.93,15.5 12,15.5M12,10.5C11.17,10.5 10.5,11.17 10.5,12C10.5,12.83 11.17,13.5 12,13.5C12.83,13.5 13.5,12.83 13.5,12C13.5,11.17 12.83,10.5 12,10.5Z",
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
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Dashboard";
    }

    [RelayCommand]
    private void NavigateToCreditors()
    {
        var vm = _serviceProvider.GetRequiredService<CreditorsViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Creditors";
    }

    [RelayCommand]
    private void NavigateToAnalytics()
    {
        var vm = _serviceProvider.GetRequiredService<AnalyticsViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Analytics";
    }

    [RelayCommand]
    private void NavigateToInventory()
    {
        var vm = _serviceProvider.GetRequiredService<InventoryViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Inventory";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        var vm = _serviceProvider.GetRequiredService<SettingsViewModel>();
        _navigationService.NavigateTo(vm);
        CurrentPageTitle = "Settings";
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _loginFacade.LogoutAsync();
        ShowLogin();
    }
}
