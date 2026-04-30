using System.Windows;
using BlueBirdsERP.Application.Abstractions;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Controls;

public partial class RecordPaymentDialog : Window
{
    public AccountPaymentRequest? Result { get; private set; }
    private readonly Guid _customerId;
    private readonly Guid _recordedBy;

    public RecordPaymentDialog(Guid customerId, Guid recordedBy, decimal outstandingBalance)
    {
        _customerId = customerId;
        _recordedBy = recordedBy;
        InitializeComponent();
        OutstandingText.Text = $"Rs. {outstandingBalance:N2}";
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(AmountBox.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Enter a valid payment amount.", "Validation");
            return;
        }

        var paymentMethod = PaymentMethodBox.SelectedIndex == 0 ? PaymentMethod.Cash : PaymentMethod.Card;

        Result = new AccountPaymentRequest(
            CustomerId: _customerId,
            InvoiceId: null,
            Amount: amount,
            PaymentMethod: paymentMethod,
            Reference: string.IsNullOrWhiteSpace(ReferenceBox.Text) ? null : ReferenceBox.Text.Trim(),
            RecordedBy: _recordedBy,
            Role: UserRole.Admin);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
