using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Controls;

public partial class CreateOneTimeCreditorDialog : Window
{
    public CreateOneTimeCreditorRequest? Result { get; private set; }
    private readonly Guid _adminUserId;

    public CreateOneTimeCreditorDialog(Guid adminUserId)
    {
        _adminUserId = adminUserId;
        InitializeComponent();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Full name is required.", "Validation");
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
        if (string.IsNullOrWhiteSpace(AddressBox.Text))
        {
            MessageBox.Show("Address is required.", "Validation");
            return;
        }

        Result = new CreateOneTimeCreditorRequest(
            FullName: NameBox.Text.Trim(),
            Phone: PhoneBox.Text.Trim(),
            WhatsAppNo: WhatsAppBox.Text.Trim(),
            Address: AddressBox.Text.Trim(),
            NicOrBusinessRegistrationNumber: string.IsNullOrWhiteSpace(NicBox.Text) ? null : NicBox.Text.Trim(),
            CreatedBy: _adminUserId,
            Role: UserRole.Admin);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
