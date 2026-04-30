using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Controls;

public partial class CreateBusinessAccountDialog : Window
{
    public CreateBusinessAccountRequest? Result { get; private set; }
    private readonly Guid _adminUserId;

    public CreateBusinessAccountDialog(Guid adminUserId)
    {
        _adminUserId = adminUserId;
        InitializeComponent();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Business name is required.", "Validation");
            return;
        }
        if (string.IsNullOrWhiteSpace(PhoneBox.Text))
        {
            MessageBox.Show("Phone number is required.", "Validation");
            return;
        }
        if (string.IsNullOrWhiteSpace(WhatsAppBox.Text))
        {
            MessageBox.Show("WhatsApp number is required.", "Validation");
            return;
        }
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

        Result = new CreateBusinessAccountRequest(
            Name: NameBox.Text.Trim(),
            Phone: PhoneBox.Text.Trim(),
            WhatsAppNo: WhatsAppBox.Text.Trim(),
            Email: string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim(),
            Address: string.IsNullOrWhiteSpace(AddressBox.Text) ? null : AddressBox.Text.Trim(),
            CreditLimit: creditLimit,
            CreditPeriodDays: creditPeriod,
            NotificationLeadDays: notificationLead,
            CreatedBy: _adminUserId,
            Role: UserRole.Admin);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
