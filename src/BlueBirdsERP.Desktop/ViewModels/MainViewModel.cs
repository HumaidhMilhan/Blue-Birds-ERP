using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;
using BlueBirdsERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static BlueBirdsERP.Desktop.ViewModels.Formatters;

namespace BlueBirdsERP.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ILoginSessionFacade _loginSession;
    private readonly DashboardPageViewModel _dashboardPage;
    private readonly PosPageViewModel _posPage;
    private readonly InventoryPageViewModel _inventoryPage;
    private readonly CreditorsPageViewModel _creditorsPage;
    private readonly ReportsPageViewModel _reportsPage;
    private readonly SettingsPageViewModel _settingsPage;
    private PageViewModelBase? _currentPage;
    private string _loginUsername = string.Empty;
    private string _password = string.Empty;
    private string _loginError = string.Empty;
    private bool _isAuthenticated;
    private string _currentUserDisplay = "Not signed in";
    private string _currentRoleDisplay = string.Empty;
    private string _statusMessage = "SQLite offline mode ready.";

    public MainViewModel(
        ILoginSessionFacade loginSession,
        PoultryProDbContext dbContext,
        IInventoryService inventoryService,
        IPOSCheckoutService checkoutService,
        ICustomerAccountService customerAccountService,
        IReportingService reportingService,
        ISystemSettingsService settingsService,
        IDatabaseManagementService databaseManagementService,
        IReceiptPdfService receiptPdfService)
    {
        _loginSession = loginSession;
        _dashboardPage = new DashboardPageViewModel(this, dbContext, databaseManagementService);
        _posPage = new PosPageViewModel(this, dbContext, checkoutService, receiptPdfService);
        _inventoryPage = new InventoryPageViewModel(this, dbContext, inventoryService);
        _creditorsPage = new CreditorsPageViewModel(this, dbContext, customerAccountService);
        _reportsPage = new ReportsPageViewModel(this, reportingService);
        _settingsPage = new SettingsPageViewModel(this, settingsService, databaseManagementService);

        NavigationItems =
        [
            new("Dashboard", "\uE80F", _dashboardPage, null),
            new("Point of Sale", "\uE7BF", _posPage, RbacPermission.PosBilling),
            new("Inventory", "\uE8D2", _inventoryPage, RbacPermission.InventoryManagement),
            new("Creditors", "\uE716", _creditorsPage, RbacPermission.PaymentRecording),
            new("Reports", "\uE9D2", _reportsPage, RbacPermission.Reporting),
            new("Settings", "\uE713", _settingsPage, RbacPermission.SystemConfiguration)
        ];

        LoginCommand = new AsyncRelayCommand(_ => SignInAsync());
        LogoutCommand = new AsyncRelayCommand(_ => LogoutAsync());
        NavigateCommand = new AsyncRelayCommand(parameter => NavigateAsync(parameter as NavigationItem));
    }

    public string ProductName { get; } = "PoultryPro ERP";
    public string CompanyName { get; } = "Blue Birds Poultry";
    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public ObservableCollection<string> VisiblePermissions { get; } = [];
    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand NavigateCommand { get; }

    public AuthenticatedUser? CurrentUser => _loginSession.CurrentUser;

    public PageViewModelBase? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string LoginUsername
    {
        get => _loginUsername;
        set => SetProperty(ref _loginUsername, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string LoginError
    {
        get => _loginError;
        private set => SetProperty(ref _loginError, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (SetProperty(ref _isAuthenticated, value))
            {
                OnPropertyChanged(nameof(IsLoginVisible));
            }
        }
    }

    public bool IsLoginVisible => !IsAuthenticated;

    public string CurrentUserDisplay
    {
        get => _currentUserDisplay;
        private set => SetProperty(ref _currentUserDisplay, value);
    }

    public string CurrentRoleDisplay
    {
        get => _currentRoleDisplay;
        private set => SetProperty(ref _currentRoleDisplay, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasPermission(RbacPermission permission)
    {
        return _loginSession.CurrentPermissions.Contains(permission);
    }

    private async Task SignInAsync()
    {
        LoginError = string.Empty;
        StatusMessage = "Signing in...";

        try
        {
            var result = await _loginSession.SignInAsync(new LoginRequest(LoginUsername, Password));
            if (!result.IsAuthenticated || result.User is null)
            {
                LoginError = result.ErrorMessage ?? "Invalid username or password.";
                StatusMessage = "Sign in failed.";
                return;
            }

            IsAuthenticated = true;
            CurrentUserDisplay = result.User.Username;
            CurrentRoleDisplay = result.User.Role.ToString().ToUpperInvariant();
            VisiblePermissions.Clear();

            foreach (var permission in result.Permissions.OrderBy(permission => permission.ToString()))
            {
                VisiblePermissions.Add(permission.ToString());
            }

            foreach (var item in NavigationItems)
            {
                item.IsVisible = item.RequiredPermission is null || result.Permissions.Contains(item.RequiredPermission.Value);
            }

            Password = string.Empty;
            StatusMessage = "Signed in. Local SQLite services are connected.";
            await NavigateAsync(NavigationItems.First(item => item.Title == "Dashboard"));
        }
        catch (Exception ex)
        {
            LoginError = ex.Message;
            StatusMessage = "Sign in failed.";
        }
    }

    private async Task LogoutAsync()
    {
        await _loginSession.LogoutAsync();
        IsAuthenticated = false;
        CurrentPage = null;
        CurrentUserDisplay = "Not signed in";
        CurrentRoleDisplay = string.Empty;
        VisiblePermissions.Clear();
        StatusMessage = "Signed out.";

        foreach (var item in NavigationItems)
        {
            item.IsActive = false;
        }
    }

    private async Task NavigateAsync(NavigationItem? item)
    {
        if (item is null || !item.IsVisible)
        {
            return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsActive = ReferenceEquals(navigationItem, item);
        }

        try
        {
            CurrentPage = item.Page;
            StatusMessage = $"Loaded {item.Title}.";
            await item.Page.LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{item.Title} failed to load: {ex.Message}";
        }
    }
}

public sealed class NavigationItem(
    string title,
    string icon,
    PageViewModelBase page,
    RbacPermission? requiredPermission) : ViewModelBase
{
    private bool _isActive;
    private bool _isVisible = true;

    public string Title { get; } = title;
    public string Icon { get; } = icon;
    public PageViewModelBase Page { get; } = page;
    public RbacPermission? RequiredPermission { get; } = requiredPermission;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

public abstract class PageViewModelBase(MainViewModel shell, string title, string icon) : ViewModelBase
{
    protected MainViewModel Shell { get; } = shell;

    public string Title { get; } = title;
    public string Icon { get; } = icon;

    public abstract Task LoadAsync();
}

public sealed class DashboardPageViewModel(
    MainViewModel shell,
    PoultryProDbContext dbContext,
    IDatabaseManagementService databaseManagementService) : PageViewModelBase(shell, "Dashboard", "\uE80F")
{
    public ObservableCollection<MetricCard> Metrics { get; } = [];
    public ObservableCollection<RecentInvoiceRow> RecentInvoices { get; } = [];
    public ObservableCollection<QueueStatusRow> QueueRows { get; } = [];

    public override async Task LoadAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var invoices = await dbContext.Invoices
            .Where(invoice => invoice.InvoiceDate >= today && invoice.InvoiceDate < tomorrow)
            .Where(invoice => invoice.PaymentStatus != PaymentStatus.Void)
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .Take(8)
            .ToListAsync();

        var activeBatchQuantities = await dbContext.Batches
            .Where(batch => batch.Status == BatchStatus.Active)
            .Select(batch => batch.RemainingQuantity)
            .ToListAsync();
        var outstandingBalances = await dbContext.Invoices
            .Where(invoice => invoice.PaymentStatus != PaymentStatus.Void && invoice.BalanceAmount > 0)
            .Select(invoice => invoice.BalanceAmount)
            .ToListAsync();
        var wastageLosses = await dbContext.WastageRecords
            .Where(record => record.WastageDate == today)
            .Select(record => record.EstimatedLoss)
            .ToListAsync();

        var stockOnHand = activeBatchQuantities.Sum();
        var creditDue = outstandingBalances.Sum();
        var wastageLoss = wastageLosses.Sum();

        Metrics.ReplaceWith(
            new MetricCard("Sales Today", Money(invoices.Sum(invoice => invoice.GrandTotal)), $"{invoices.Count} invoices", "\uE8C7"),
            new MetricCard("Stock On Hand", $"{stockOnHand:N2}", "Active batch quantity", "\uE8D2"),
            new MetricCard("Credit Due", Money(creditDue), "Open receivables", "\uE8A1"),
            new MetricCard("Wastage Loss", Money(wastageLoss), "Recorded today", "\uE74D"));

        RecentInvoices.ReplaceWith(invoices.Select(invoice => new RecentInvoiceRow(
            invoice.InvoiceNumber,
            invoice.SaleChannel.ToString(),
            Money(invoice.GrandTotal),
            invoice.PaymentStatus.ToString())));

        QueueRows.Clear();
        if (Shell.CurrentUser is { Role: UserRole.Admin } user)
        {
            var queue = await databaseManagementService.GetSyncQueueStatusAsync(new AdminOperationRequest(user.UserId, user.Role));
            QueueRows.ReplaceWith(
                new QueueStatusRow("Pending", queue.Pending),
                new QueueStatusRow("Processing", queue.Processing),
                new QueueStatusRow("Completed", queue.Completed),
                new QueueStatusRow("Failed", queue.Failed));
        }
    }
}

public sealed class PosPageViewModel : PageViewModelBase
{
    private readonly PoultryProDbContext _dbContext;
    private readonly IPOSCheckoutService _checkoutService;
    private readonly IReceiptPdfService _receiptPdfService;
    private ProductSaleRow? _selectedProduct;
    private CustomerOption? _selectedCustomer;
    private SaleChannel _selectedSaleChannel = SaleChannel.Retail;
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    private decimal _quantity = 1m;
    private decimal _cashAmount;
    private decimal _cardAmount;
    private decimal _creditAmount;
    private string _manualDueDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
    private string _message = "Select a product and add it to the invoice.";
    private Guid? _lastInvoiceId;

    public ObservableCollection<ProductSaleRow> Products { get; } = [];
    public ObservableCollection<CartLineViewModel> CartLines { get; } = [];
    public ObservableCollection<CustomerOption> Customers { get; } = [];
    public ObservableCollection<SaleChannel> SaleChannels { get; } = [SaleChannel.Retail, SaleChannel.Wholesale];
    public ObservableCollection<PaymentMethod> PaymentMethods { get; } = [];
    public ICommand AddSelectedProductCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand ClearCartCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand PreviewReceiptCommand { get; }

    public PosPageViewModel(
        MainViewModel shell,
        PoultryProDbContext dbContext,
        IPOSCheckoutService checkoutService,
        IReceiptPdfService receiptPdfService) : base(shell, "Point of Sale", "\uE7BF")
    {
        _dbContext = dbContext;
        _checkoutService = checkoutService;
        _receiptPdfService = receiptPdfService;
        AddSelectedProductCommand = new RelayCommand(_ => AddSelectedProduct());
        RemoveLineCommand = new RelayCommand(parameter => RemoveLine(parameter as CartLineViewModel));
        ClearCartCommand = new RelayCommand(_ => ClearCart());
        CheckoutCommand = new AsyncRelayCommand(_ => CheckoutAsync());
        PreviewReceiptCommand = new AsyncRelayCommand(_ => PreviewReceiptAsync(), _ => _lastInvoiceId.HasValue);
        RefreshPaymentMethods();
    }

    public ProductSaleRow? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public CustomerOption? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetProperty(ref _selectedCustomer, value);
    }

    public SaleChannel SelectedSaleChannel
    {
        get => _selectedSaleChannel;
        set
        {
            if (SetProperty(ref _selectedSaleChannel, value))
            {
                RefreshPaymentMethods();
            }
        }
    }

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                AutoFillPaymentAmounts();
            }
        }
    }

    public decimal Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public decimal CashAmount
    {
        get => _cashAmount;
        set => SetProperty(ref _cashAmount, value);
    }

    public decimal CardAmount
    {
        get => _cardAmount;
        set => SetProperty(ref _cardAmount, value);
    }

    public decimal CreditAmount
    {
        get => _creditAmount;
        set => SetProperty(ref _creditAmount, value);
    }

    public string ManualDueDate
    {
        get => _manualDueDate;
        set => SetProperty(ref _manualDueDate, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public decimal CartTotal => CartLines.Sum(line => line.LineTotal);
    public string CartTotalText => Money(CartTotal);

    public override async Task LoadAsync()
    {
        var products = await _dbContext.Products
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .ToListAsync();
        var activeBatches = await _dbContext.Batches
            .Where(batch => batch.Status == BatchStatus.Active)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.PurchaseDate)
            .ToListAsync();

        Products.ReplaceWith(products.Select(product =>
        {
            var productBatches = activeBatches.Where(batch => batch.ProductId == product.ProductId).ToList();
            var firstBatch = productBatches.FirstOrDefault(batch => batch.RemainingQuantity > 0);
            return new ProductSaleRow(
                product.ProductId,
                firstBatch?.BatchId,
                product.Name,
                product.UnitOfMeasure,
                product.SellingPrice,
                productBatches.Sum(batch => batch.RemainingQuantity),
                firstBatch is null ? "No active batch" : firstBatch.BatchId.ToString("N")[..8].ToUpperInvariant());
        }));

        Customers.ReplaceWith(await _dbContext.Customers
            .Where(customer => customer.AccountType != AccountType.None)
            .OrderBy(customer => customer.Name)
            .Select(customer => new CustomerOption(customer.CustomerId, customer.Name, customer.AccountType.ToString()))
            .ToListAsync());

        SelectedProduct ??= Products.FirstOrDefault();
        SelectedCustomer ??= Customers.FirstOrDefault();
        AutoFillPaymentAmounts();
    }

    private void AddSelectedProduct()
    {
        if (SelectedProduct is null)
        {
            Message = "Select a product first.";
            return;
        }

        if (!SelectedProduct.BatchId.HasValue)
        {
            Message = "Selected product has no active batch.";
            return;
        }

        if (Quantity <= 0)
        {
            Message = "Quantity must be greater than zero.";
            return;
        }

        if (Quantity > SelectedProduct.Stock)
        {
            Message = "Quantity exceeds available stock.";
            return;
        }

        CartLines.Add(new CartLineViewModel(
            SelectedProduct.ProductId,
            SelectedProduct.BatchId.Value,
            SelectedProduct.ProductName,
            SelectedProduct.Unit,
            Quantity,
            SelectedProduct.Price));
        RefreshCartTotals();
        Message = $"{SelectedProduct.ProductName} added.";
    }

    private void RemoveLine(CartLineViewModel? line)
    {
        if (line is null)
        {
            return;
        }

        CartLines.Remove(line);
        RefreshCartTotals();
    }

    private void ClearCart()
    {
        CartLines.Clear();
        RefreshCartTotals();
        Message = "Invoice cleared.";
    }

    private async Task CheckoutAsync()
    {
        if (Shell.CurrentUser is null)
        {
            Message = "Sign in before checkout.";
            return;
        }

        if (CartLines.Count == 0)
        {
            Message = "Add at least one item.";
            return;
        }

        var requiresCustomer = SelectedPaymentMethod is PaymentMethod.Credit or PaymentMethod.Mixed;
        if (requiresCustomer && SelectedCustomer is null)
        {
            Message = "Credit and mixed wholesale invoices require a customer.";
            return;
        }

        DateTime? dueDate = null;
        if (requiresCustomer && !string.IsNullOrWhiteSpace(ManualDueDate))
        {
            if (!DateTime.TryParse(ManualDueDate, out var parsedDueDate))
            {
                Message = "Due date must be a valid date.";
                return;
            }

            dueDate = parsedDueDate.Date;
        }

        try
        {
            var result = await _checkoutService.CheckoutAsync(new CheckoutRequest(
                SelectedSaleChannel,
                requiresCustomer ? SelectedCustomer?.CustomerId : null,
                Shell.CurrentUser.UserId,
                SelectedPaymentMethod,
                CartLines.Select(line => new CheckoutLineItem(line.ProductId, line.BatchId, line.Quantity, line.UnitPrice, 0m)).ToList(),
                CashAmount,
                CardAmount,
                CreditAmount,
                dueDate,
                Notes: "Created from WPF POS"));

            _lastInvoiceId = result.InvoiceId;
            Message = $"Created {result.InvoiceNumber}. Balance {Money(result.BalanceAmount)}.";
            ClearCart();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private async Task PreviewReceiptAsync()
    {
        if (Shell.CurrentUser is null || !_lastInvoiceId.HasValue)
        {
            Message = "Create an invoice before previewing a receipt.";
            return;
        }

        try
        {
            var receipt = await _receiptPdfService.GenerateInvoiceReceiptPdfAsync(
                new ReceiptPdfRequest(_lastInvoiceId.Value, Shell.CurrentUser.UserId, Shell.CurrentUser.Role));
            var path = Path.Combine(Path.GetTempPath(), receipt.FileName);
            await File.WriteAllBytesAsync(path, receipt.PdfBytes);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Message = $"Receipt opened: {receipt.InvoiceNumber}.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private void RefreshPaymentMethods()
    {
        PaymentMethods.ReplaceWith(_checkoutService.GetAllowedPaymentMethods(SelectedSaleChannel));
        if (!PaymentMethods.Contains(SelectedPaymentMethod))
        {
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
        }

        AutoFillPaymentAmounts();
    }

    private void RefreshCartTotals()
    {
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
        AutoFillPaymentAmounts();
    }

    private void AutoFillPaymentAmounts()
    {
        if (SelectedPaymentMethod == PaymentMethod.Cash)
        {
            CashAmount = CartTotal;
            CardAmount = 0m;
            CreditAmount = 0m;
        }
        else if (SelectedPaymentMethod == PaymentMethod.Card)
        {
            CashAmount = 0m;
            CardAmount = CartTotal;
            CreditAmount = 0m;
        }
        else if (SelectedPaymentMethod == PaymentMethod.Credit)
        {
            CashAmount = 0m;
            CardAmount = 0m;
            CreditAmount = CartTotal;
        }
    }
}

public sealed class InventoryPageViewModel : PageViewModelBase
{
    private readonly PoultryProDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private ProductStockRow? _selectedProduct;
    private decimal _newBatchQuantity = 10m;
    private decimal _newBatchCost;
    private string _newBatchExpiry = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd");
    private string _message = "Manual purchases create active batches.";

    public ObservableCollection<ProductStockRow> Products { get; } = [];
    public ObservableCollection<BatchRow> Batches { get; } = [];
    public ObservableCollection<string> Alerts { get; } = [];
    public ICommand RecordBatchCommand { get; }

    public InventoryPageViewModel(
        MainViewModel shell,
        PoultryProDbContext dbContext,
        IInventoryService inventoryService) : base(shell, "Inventory", "\uE8D2")
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        RecordBatchCommand = new AsyncRelayCommand(_ => RecordBatchAsync());
    }

    public ProductStockRow? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public decimal NewBatchQuantity
    {
        get => _newBatchQuantity;
        set => SetProperty(ref _newBatchQuantity, value);
    }

    public decimal NewBatchCost
    {
        get => _newBatchCost;
        set => SetProperty(ref _newBatchCost, value);
    }

    public string NewBatchExpiry
    {
        get => _newBatchExpiry;
        set => SetProperty(ref _newBatchExpiry, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public override async Task LoadAsync()
    {
        var stock = await _inventoryService.GetProductStockLevelsAsync();
        Products.ReplaceWith(stock.Select(row => new ProductStockRow(
            row.ProductId,
            row.ProductName,
            row.UnitOfMeasure,
            row.RemainingQuantity,
            row.ReorderLevel)));

        var batchData = await _dbContext.Batches
            .Join(_dbContext.Products, batch => batch.ProductId, product => product.ProductId, (batch, product) => new
            {
                batch.BatchId,
                ProductName = product.Name,
                batch.RemainingQuantity,
                product.UnitOfMeasure,
                batch.CostPrice,
                batch.ExpiryDate,
                batch.Status
            })
            .OrderBy(row => row.ProductName)
            .ToListAsync();
        var batches = batchData.Select(row => new BatchRow(
            row.BatchId.ToString("N")[..8].ToUpperInvariant(),
            row.ProductName,
            $"{row.RemainingQuantity:N2} {row.UnitOfMeasure}",
            Money(row.CostPrice),
            row.ExpiryDate.HasValue ? row.ExpiryDate.Value.ToString("yyyy-MM-dd") : "-",
            row.Status.ToString()));
        Batches.ReplaceWith(batches);

        var alerts = await _inventoryService.GetInventoryAlertsAsync(DateOnly.FromDateTime(DateTime.Today), 3);
        Alerts.ReplaceWith(alerts.Select(alert => alert.Message));
        SelectedProduct ??= Products.FirstOrDefault();
    }

    private async Task RecordBatchAsync()
    {
        if (Shell.CurrentUser is null)
        {
            Message = "Sign in before recording inventory.";
            return;
        }

        if (SelectedProduct is null)
        {
            Message = "Select a product first.";
            return;
        }

        if (!DateTime.TryParse(NewBatchExpiry, out var expiry))
        {
            Message = "Expiry date must be valid.";
            return;
        }

        try
        {
            await _inventoryService.RecordManualBatchPurchaseAsync(new ManualBatchPurchaseRequest(
                SelectedProduct.ProductId,
                DateTime.Today,
                expiry,
                NewBatchQuantity,
                NewBatchCost,
                Shell.CurrentUser.UserId,
                Shell.CurrentUser.Role));
            Message = "Manual purchase batch recorded.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}

public sealed class CreditorsPageViewModel : PageViewModelBase
{
    private readonly PoultryProDbContext _dbContext;
    private readonly ICustomerAccountService _customerAccountService;
    private CreditorInvoiceRow? _selectedInvoice;
    private decimal _paymentAmount;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private string _message = "Select an outstanding invoice to record a payment.";

    public ObservableCollection<CreditorInvoiceRow> OutstandingInvoices { get; } = [];
    public ObservableCollection<DebtorBucketRow> DebtorBuckets { get; } = [];
    public ObservableCollection<PaymentMethod> PaymentMethods { get; } = [PaymentMethod.Cash, PaymentMethod.Card];
    public ICommand RecordPaymentCommand { get; }

    public CreditorsPageViewModel(
        MainViewModel shell,
        PoultryProDbContext dbContext,
        ICustomerAccountService customerAccountService) : base(shell, "Creditors", "\uE716")
    {
        _dbContext = dbContext;
        _customerAccountService = customerAccountService;
        RecordPaymentCommand = new AsyncRelayCommand(_ => RecordPaymentAsync());
    }

    public CreditorInvoiceRow? SelectedInvoice
    {
        get => _selectedInvoice;
        set
        {
            if (SetProperty(ref _selectedInvoice, value) && value is not null)
            {
                PaymentAmount = value.BalanceAmount;
            }
        }
    }

    public decimal PaymentAmount
    {
        get => _paymentAmount;
        set => SetProperty(ref _paymentAmount, value);
    }

    public PaymentMethod PaymentMethod
    {
        get => _paymentMethod;
        set => SetProperty(ref _paymentMethod, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public override async Task LoadAsync()
    {
        var outstanding = await _dbContext.Invoices
            .Where(invoice => invoice.CustomerId.HasValue && invoice.PaymentStatus != PaymentStatus.Void && invoice.BalanceAmount > 0)
            .Join(_dbContext.Customers, invoice => invoice.CustomerId, customer => customer.CustomerId, (invoice, customer) => new CreditorInvoiceRow(
                invoice.InvoiceId,
                customer.CustomerId,
                customer.Name,
                customer.AccountType.ToString(),
                invoice.InvoiceNumber,
                invoice.DueDate.HasValue ? invoice.DueDate.Value.ToString("yyyy-MM-dd") : "-",
                invoice.BalanceAmount,
                Money(invoice.BalanceAmount),
                invoice.PaymentStatus.ToString()))
            .OrderBy(row => row.DueDate)
            .ToListAsync();

        OutstandingInvoices.ReplaceWith(outstanding);
        SelectedInvoice ??= OutstandingInvoices.FirstOrDefault();

        var aging = await _customerAccountService.GenerateDebtorAgingReportAsync(DateOnly.FromDateTime(DateTime.Today));
        DebtorBuckets.ReplaceWith(aging.Buckets.Select(bucket => new DebtorBucketRow(bucket.Name, Money(bucket.OutstandingBalance), bucket.Invoices.Count)));
    }

    private async Task RecordPaymentAsync()
    {
        if (Shell.CurrentUser is null)
        {
            Message = "Sign in before recording payments.";
            return;
        }

        if (SelectedInvoice is null)
        {
            Message = "Select an invoice first.";
            return;
        }

        try
        {
            var result = await _customerAccountService.RecordAccountPaymentAsync(new AccountPaymentRequest(
                SelectedInvoice.CustomerId,
                SelectedInvoice.InvoiceId,
                PaymentAmount,
                PaymentMethod,
                "WPF payment",
                Shell.CurrentUser.UserId,
                Shell.CurrentUser.Role));
            Message = $"Applied {Money(result.AmountApplied)}. Remaining {Money(result.RemainingOutstanding)}.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}

public sealed class ReportsPageViewModel : PageViewModelBase
{
    private readonly IReportingService _reportingService;
    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private string _message = "Run a report to refresh operational figures.";

    public ObservableCollection<MetricCard> Metrics { get; } = [];
    public ObservableCollection<StockReportRow> StockRows { get; } = [];
    public ObservableCollection<BatchMovementReportRow> BatchRows { get; } = [];
    public ObservableCollection<AuditReportRow> AuditRows { get; } = [];
    public ICommand RunReportCommand { get; }

    public ReportsPageViewModel(
        MainViewModel shell,
        IReportingService reportingService) : base(shell, "Reports", "\uE9D2")
    {
        _reportingService = reportingService;
        RunReportCommand = new AsyncRelayCommand(_ => LoadAsync());
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public override async Task LoadAsync()
    {
        if (Shell.CurrentUser is not { Role: UserRole.Admin } user)
        {
            Message = "Reports require Admin access.";
            return;
        }

        try
        {
            var result = await _reportingService.GenerateOperationalReportAsync(new OperationalReportRequest(
                DateOnly.FromDateTime(FromDate),
                DateOnly.FromDateTime(ToDate),
                user.UserId,
                user.Role));

            Metrics.ReplaceWith(
                new MetricCard("Total Sales", Money(result.Sales.TotalSales), $"{result.Sales.InvoiceCount} invoices", "\uE8C7"),
                new MetricCard("Gross Profit", Money(result.Profit.GrossProfit), $"COGS {Money(result.Profit.CostOfGoodsSold)}", "\uE9D2"),
                new MetricCard("Wastage", Money(result.Wastage.TotalWastageValue), $"{result.Wastage.RecordCount} records", "\uE74D"),
                new MetricCard("Refunds", Money(result.SalesReturns.TotalRefundAmount), $"{result.SalesReturns.ReturnCount} returns", "\uE8A7"));

            StockRows.ReplaceWith(result.StockOnHand.Select(row => new StockReportRow(
                row.ProductName,
                $"{row.RemainingQuantity:N2} {row.UnitOfMeasure}",
                $"{row.ReorderLevel:N2} {row.UnitOfMeasure}")));

            BatchRows.ReplaceWith(result.BatchMovements.Select(row => new BatchMovementReportRow(
                row.ProductName,
                row.PurchasedQuantity,
                row.SoldQuantity,
                row.WastedQuantity,
                row.RemainingQuantity)));

            AuditRows.ReplaceWith(result.AuditActivity.Take(10).Select(row => new AuditReportRow(
                row.Timestamp.ToLocalTime().ToString("HH:mm"),
                row.Module,
                row.Action)));

            Message = "Report generated from local SQLite.";
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }
}

public sealed class SettingsPageViewModel : PageViewModelBase
{
    private readonly ISystemSettingsService _settingsService;
    private readonly IDatabaseManagementService _databaseManagementService;
    private string _settingKey = "receipt.companyName";
    private string _settingValue = "Blue Birds Poultry";
    private string _message = "Admin-only system settings.";

    public ObservableCollection<SystemSettingRow> Settings { get; } = [];
    public ObservableCollection<DatabaseActionRow> DatabaseRows { get; } = [];
    public ICommand SaveSettingCommand { get; }
    public ICommand TestSqliteCommand { get; }
    public ICommand BackupDatabaseCommand { get; }

    public SettingsPageViewModel(
        MainViewModel shell,
        ISystemSettingsService settingsService,
        IDatabaseManagementService databaseManagementService) : base(shell, "Settings", "\uE713")
    {
        _settingsService = settingsService;
        _databaseManagementService = databaseManagementService;
        SaveSettingCommand = new AsyncRelayCommand(_ => SaveSettingAsync());
        TestSqliteCommand = new AsyncRelayCommand(_ => TestSqliteAsync());
        BackupDatabaseCommand = new AsyncRelayCommand(_ => BackupDatabaseAsync());
    }

    public string SettingKey
    {
        get => _settingKey;
        set => SetProperty(ref _settingKey, value);
    }

    public string SettingValue
    {
        get => _settingValue;
        set => SetProperty(ref _settingValue, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public override async Task LoadAsync()
    {
        if (Shell.CurrentUser is not { Role: UserRole.Admin } user)
        {
            Message = "Settings require Admin access.";
            return;
        }

        var settings = await _settingsService.GetSettingsAsync(new SystemSettingsQuery(user.UserId, user.Role));
        Settings.ReplaceWith(settings.Select(setting => new SystemSettingRow(
            setting.Key,
            setting.IsSecret ? "********" : setting.Value,
            setting.ValueType.ToString(),
            setting.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))));

        var queue = await _databaseManagementService.GetSyncQueueStatusAsync(new AdminOperationRequest(user.UserId, user.Role));
        DatabaseRows.ReplaceWith(
            new DatabaseActionRow("SQLite", "Primary offline database", "Ready"),
            new DatabaseActionRow("Online Queue", $"Pending {queue.Pending}, failed {queue.Failed}", "Local first"),
            new DatabaseActionRow("Backups", "Copies SQLite database to configured backup folder", "Manual"));
    }

    private async Task SaveSettingAsync()
    {
        if (Shell.CurrentUser is not { Role: UserRole.Admin } user)
        {
            Message = "Settings require Admin access.";
            return;
        }

        try
        {
            await _settingsService.UpdateSettingsAsync(new UpdateSystemSettingsRequest(
                user.UserId,
                user.Role,
                [new SystemSettingUpdate(SettingKey, SettingValue, SystemSettingValueType.String)]));
            Message = "Setting saved and audited.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private async Task TestSqliteAsync()
    {
        if (Shell.CurrentUser is not { Role: UserRole.Admin } user)
        {
            return;
        }

        var result = await _databaseManagementService.TestSqliteConnectionAsync(new AdminOperationRequest(user.UserId, user.Role));
        Message = result.Message;
        await LoadAsync();
    }

    private async Task BackupDatabaseAsync()
    {
        if (Shell.CurrentUser is not { Role: UserRole.Admin } user)
        {
            return;
        }

        var result = await _databaseManagementService.BackupSqliteDatabaseAsync(new DatabaseBackupRequest(user.UserId, user.Role));
        Message = result.Succeeded ? $"Backup created: {result.BackupPath}" : result.Message;
        await LoadAsync();
    }
}

public sealed record MetricCard(string Label, string Value, string Detail, string Icon);
public sealed record RecentInvoiceRow(string InvoiceNumber, string Channel, string Amount, string Status);
public sealed record QueueStatusRow(string Status, int Count);
public sealed record ProductSaleRow(Guid ProductId, Guid? BatchId, string ProductName, string Unit, decimal Price, decimal Stock, string BatchReference)
{
    public string PriceText => Money(Price);
    public string StockText => $"{Stock:N2} {Unit}";
}

public sealed record CustomerOption(Guid CustomerId, string Name, string AccountType);
public sealed record CartLineViewModel(Guid ProductId, Guid BatchId, string ProductName, string Unit, decimal Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
    public string QuantityText => $"{Quantity:N2} {Unit}";
    public string UnitPriceText => Money(UnitPrice);
    public string LineTotalText => Money(LineTotal);
}

public sealed record ProductStockRow(Guid ProductId, string ProductName, string Unit, decimal RemainingQuantity, decimal ReorderLevel)
{
    public string RemainingText => $"{RemainingQuantity:N2} {Unit}";
    public string ReorderText => $"{ReorderLevel:N2} {Unit}";
}

public sealed record BatchRow(string Batch, string ProductName, string Remaining, string Cost, string ExpiryDate, string Status);
public sealed record CreditorInvoiceRow(Guid InvoiceId, Guid CustomerId, string CustomerName, string AccountType, string InvoiceNumber, string DueDate, decimal BalanceAmount, string BalanceText, string Status);
public sealed record DebtorBucketRow(string Bucket, string Amount, int InvoiceCount);
public sealed record StockReportRow(string ProductName, string Remaining, string ReorderLevel);
public sealed record BatchMovementReportRow(string ProductName, decimal Purchased, decimal Sold, decimal Wasted, decimal Remaining);
public sealed record AuditReportRow(string Time, string Module, string Action);
public sealed record SystemSettingRow(string Key, string Value, string Type, string UpdatedAt);
public sealed record DatabaseActionRow(string Name, string Detail, string Status);

internal static class CollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, params T[] items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}

internal static class Formatters
{
    public static string Money(decimal amount)
    {
        return $"Rs. {amount:N2}";
    }
}
