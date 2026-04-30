using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Controls;

public partial class EditTermsDialog : Window
{
    public UpdateBusinessAccountTermsRequest? Result { get; private set; }
    private readonly Guid _customerId;
    private readonly Guid _updatedBy;

    public EditTermsDialog(Guid customerId, Guid updatedBy, decimal currentCreditLimit, int currentCreditPeriod, int currentNotificationLead)
    {
        _customerId = customerId;
        _updatedBy = updatedBy;
        InitializeComponent();
        CreditLimitBox.Text = currentCreditLimit.ToString("N2");
        CreditPeriodBox.Text = currentCreditPeriod.ToString();
        NotificationLeadBox.Text = currentNotificationLead.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(CreditLimitBox.Text, out var creditLimit) || creditLimit < 0)
        {
            MessageBox.Show("Enter a valid credit limit.", "Validation");
            return;
        }
        if (!int.TryParse(CreditPeriodBox.Text, out var creditPeriod) || creditPeriod <= 0)
        {
            MessageBox.Show("Enter a valid credit period.", "Validation");
            return;
        }
        if (!int.TryParse(NotificationLeadBox.Text, out var notificationLead) || notificationLead < 0)
        {
            MessageBox.Show("Enter a valid notification lead period.", "Validation");
            return;
        }

        Result = new UpdateBusinessAccountTermsRequest(
            CustomerId: _customerId,
            CreditLimit: creditLimit,
            CreditPeriodDays: creditPeriod,
            NotificationLeadDays: notificationLead,
            UpdatedBy: _updatedBy,
            Role: UserRole.Admin);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
