using System.Windows;
using System.Windows.Controls;
using BlueBirdsERP.Desktop.ViewModels;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Views;

public partial class PosCheckoutView : UserControl
{
    public PosCheckoutView()
    {
        InitializeComponent();
    }

    private void PaymentCash_Checked(object sender, RoutedEventArgs e) => SetPaymentMethod(PaymentMethod.Cash);
    private void PaymentCard_Checked(object sender, RoutedEventArgs e) => SetPaymentMethod(PaymentMethod.Card);
    private void PaymentCredit_Checked(object sender, RoutedEventArgs e) => SetPaymentMethod(PaymentMethod.Credit);
    private void PaymentMixed_Checked(object sender, RoutedEventArgs e) => SetPaymentMethod(PaymentMethod.Mixed);

    private void SetPaymentMethod(PaymentMethod method)
    {
        if (DataContext is PosCheckoutViewModel vm)
            vm.SelectedPaymentMethod = method;
    }
}
