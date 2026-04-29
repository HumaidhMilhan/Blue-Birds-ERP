using CommunityToolkit.Mvvm.ComponentModel;

namespace BlueBirdsERP.Desktop.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    public virtual Task LoadAsync() => Task.CompletedTask;
}
