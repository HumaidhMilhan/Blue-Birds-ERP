using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BlueBirdsERP.Desktop.ViewModels;

namespace BlueBirdsERP.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        PasswordBox.PreviewKeyDown += PasswordBox_PreviewKeyDown;
    }

    private void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        AttemptSignIn();
    }

    private void PasswordBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AttemptSignIn();
            e.Handled = true;
        }
    }

    private void AttemptSignIn()
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.SignInCommand.Execute(PasswordBox.Password);
        }
    }
}
