using System.Collections.ObjectModel;
using BlueBirdsERP.Domain.BusinessRules;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.ViewModels;

public sealed class PosWorkspaceViewModel
{
    public ObservableCollection<string> SaleChannels { get; } =
    [
        SaleChannel.Retail.ToString(),
        SaleChannel.Wholesale.ToString()
    ];

    public string SelectedSaleChannel { get; } = SaleChannel.Wholesale.ToString();

    public ObservableCollection<string> RetailPaymentMethods { get; } =
    [
        .. PoultryBusinessRules.GetAllowedPaymentMethods(SaleChannel.Retail).Select(method => method.ToString())
    ];

    public ObservableCollection<string> WholesalePaymentMethods { get; } =
    [
        .. PoultryBusinessRules.GetAllowedPaymentMethods(SaleChannel.Wholesale).Select(method => method.ToString())
    ];

    public ObservableCollection<string> BatchPickerFields { get; } =
    [
        "Batch reference",
        "Purchase date",
        "Expiry date",
        "Remaining quantity"
    ];

    public ObservableCollection<string> CreditPanelFields { get; } =
    [
        "Outstanding balance",
        "Available credit",
        "Overdue invoice count",
        "Last payment date"
    ];

    public ObservableCollection<string> AccountManagementFields { get; } =
    [
        "Business Account: credit limit, credit period, notification lead period",
        "One-Time Creditor: full name, phone, WhatsApp number, address, NIC/business registration",
        "Admin edits: audit logged and unpaid due dates recalculated"
    ];

    public ObservableCollection<string> PaymentHistoryColumns { get; } =
    [
        "Invoice no.",
        "Date",
        "Amount",
        "Status"
    ];

    public ObservableCollection<string> InventoryRules { get; } =
    [
        "Weight-based products use Kg or g only.",
        "Unit-based products use pieces only.",
        "Manual batch purchase records product, purchase date, quantity, cost price, and expiry date.",
        "Stock levels aggregate active, non-expired batches.",
        "Wastage reduces batch remaining quantity and records estimated loss.",
        "Low-stock and near-expiry alerts use configurable thresholds.",
        "Batch history reports purchased, sold, wasted, remaining, and status."
    ];
}
