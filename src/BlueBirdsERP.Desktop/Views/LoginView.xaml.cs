using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BlueBirdsERP.Desktop.ViewModels;

namespace BlueBirdsERP.Desktop.Views;

public partial class LoginView : UserControl
{
    private bool _isPasswordVisible = false;

    public LoginView()
    {
        InitializeComponent();
        PasswordBox.PreviewKeyDown += PasswordBox_PreviewKeyDown;
        VisiblePasswordBox.PreviewKeyDown += PasswordBox_PreviewKeyDown;
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
            var password = _isPasswordVisible ? VisiblePasswordBox.Text : PasswordBox.Password;
            vm.SignInCommand.Execute(password);
        }
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        if (_isPasswordVisible)
        {
            VisiblePasswordBox.Text = PasswordBox.Password;
            VisiblePasswordBox.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            EyeIcon.Data = Geometry.Parse("M12,9A3,3 0 0,1 15,12A3,3 0 0,1 12,15A3,3 0 0,1 9,12A3,3 0 0,1 12,9M12,4.5C17,4.5 21.27,7.61 23,12C21.27,16.39 17,19.5 12,19.5C7,19.5 2.73,16.39 1,12C2.73,7.61 7,4.5 12,4.5M3.18,12C4.83,15.36 8.24,17.5 12,17.5C15.76,17.5 19.17,15.36 20.82,12C19.17,8.64 15.76,6.5 12,6.5C8.24,6.5 4.83,8.64 3.18,12Z");
        }
        else
        {
            PasswordBox.Password = VisiblePasswordBox.Text;
            VisiblePasswordBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            EyeIcon.Data = Geometry.Parse("M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z");
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isPasswordVisible) VisiblePasswordBox.Text = PasswordBox.Password;
    }

    private void VisiblePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPasswordVisible) PasswordBox.Password = VisiblePasswordBox.Text;
    }
}
