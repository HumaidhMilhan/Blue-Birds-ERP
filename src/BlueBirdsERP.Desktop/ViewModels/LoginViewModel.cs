using BlueBirdsERP.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ILoginSessionFacade _loginFacade;
    private readonly IApplicationBootstrapService _bootstrapService;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _bootstrapMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _hasBootstrapMessage;

    public Action<LoginResult>? LoginSucceeded { get; set; }

    public LoginViewModel(
        ILoginSessionFacade loginFacade,
        IApplicationBootstrapService bootstrapService)
    {
        _loginFacade = loginFacade;
        _bootstrapService = bootstrapService;
    }

    [RelayCommand]
    private async Task SignInAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Please enter username and password.";
            HasError = true;
            return;
        }

        IsBusy = true;
        HasError = false;

        try
        {
            var result = await _loginFacade.SignInAsync(
                new LoginRequest(Username.Trim(), password));

            if (result.IsAuthenticated && result.User != null)
            {
                LoginSucceeded?.Invoke(result);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid username or password.";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BootstrapAsync()
    {
        IsBusy = true;
        HasError = false;
        HasBootstrapMessage = false;

        try
        {
            var result = await _bootstrapService.EnsureDevelopmentBootstrapAsync();
            if (result.Created)
            {
                BootstrapMessage = $"Admin account created. Username: {result.Username}";
                HasBootstrapMessage = true;
                Username = result.Username;
            }
            else
            {
                BootstrapMessage = $"Admin account already exists. Username: {result.Username}";
                HasBootstrapMessage = true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Bootstrap failed: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
