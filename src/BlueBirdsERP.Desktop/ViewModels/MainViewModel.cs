using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlueBirdsERP.Desktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public string ProductName { get; } = "PoultryPro ERP";
    public string CompanyName { get; } = "Blue Birds Poultry";
    public string CurrentSession { get; } = "Authentication module pending";
    public string DashboardSubtitle { get; } = "Initial shell for POS, inventory, credit, reporting, notifications, and administration.";

    public ObservableCollection<ModuleTile> Modules { get; } =
    [
        new("POS", "Retail and wholesale billing", "Manual batch selection, mixed wholesale payments, invoice printing, and offline checkout path.", "Scaffolded"),
        new("Inventory", "Batch stock control", "GRN-created batches, low-stock alerts, expiry tracking, and wastage recording.", "Scaffolded"),
        new("Credit", "Debtors and payments", "Business accounts, soft credit-limit alerts, aging, payment history, and credit notes.", "Scaffolded"),
        new("Purchasing", "Suppliers and GRNs", "Supplier profiles, purchase orders, partial GRNs, batch creation, and purchase returns.", "Scaffolded"),
        new("WhatsApp", "Twilio notifications", "Payment reminders, overdue alerts, retry log, and owner daily sales summary.", "Adapter stub"),
        new("Admin", "Security and configuration", "RBAC, session timeout, user management, encrypted settings, reports, and audit log.", "Scaffolded")
    ];

    public ObservableCollection<string> Guardrails { get; } =
    [
        "Retail checkout allows Cash and Card only.",
        "Wholesale checkout allows Cash, Card, Credit, and Mixed payments.",
        "Batch stock must never be deducted below zero.",
        "Customer sales returns become wastage and are not restocked.",
        "Credit-limit alerts are persistent but non-blocking.",
        "Financial and configuration operations require immutable audit logging."
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ModuleTile(
    string Name,
    string Area,
    string Description,
    string Status);

